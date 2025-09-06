using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend;
using BotLib.Generated;
using BotLib.Utils;
using Microsoft.Extensions.Logging;
using NQ;
using NQ.Interfaces;
using Orleans;

public class MarketService : IMarketService
{
    private readonly IClusterClient _orleans;
    private readonly IGameplayBank _gameplayBank;
    private readonly IRecipeService _recipeService;
    private readonly ConfigService _configService;
    private readonly ILogger<MarketService> _logger;
    private readonly BotConnectionManager _botConnectionManager;

    public MarketService(
        ILogger<MarketService> logger,
        ConfigService configService,
        IClusterClient clusterClient,
        IGameplayBank gameplayBank,
        IRecipeService recipeService,
        BotConnectionManager botConnectionManager
        )
    {
        _orleans = clusterClient;
        _gameplayBank = gameplayBank;
        _configService = configService;
        _recipeService = recipeService;
        _logger = logger;
        _botConnectionManager = botConnectionManager;
    }

    public async Task CreateItem(ulong itemTypeId, long quantity)
    {
        var inventoryGrain = _orleans.GetInventoryGrain(Mod.bot.PlayerId);


        var itemAndQuantity = new ItemAndQuantity
        {
            item = _gameplayBank.GetDefinition(itemTypeId).AsItemInfo(),
            quantity = _gameplayBank.QuantityFromGDValue(itemTypeId, quantity),
        };
        var list = new ItemAndQuantityList();
        list.content.Add(itemAndQuantity);

        await RetryHelper.RetryOnExceptionAsync(
            async () =>
            {
                await Mod.bot.Req.BotGiveItems(list);
                _logger.LogInformation("Item successfully created in the bot's inventory.");
            },
            _botConnectionManager.IsDisconnectedException,
            _botConnectionManager.ReconnectBotAsync
            );
    }

    public async Task PlaceMarketOrder(ulong marketId, ulong itemTypeId, long quantity, double unitPrice, bool sell = false, bool fromMarketContainer = false)
    {
        quantity = sell ? quantity * -1 : quantity;

        _logger.LogInformation($"Placing market order for item {itemTypeId} in market {marketId}. Quantity: {quantity}, Price per unit: {unitPrice}.");

        MarketRequest marketRequest = new MarketRequest
        {
            marketId = marketId,
            itemType = itemTypeId,
            buyQuantity = quantity, // Use a positive value for buy order, negative for sell
            expirationDate = DateTime.Now.AddDays(300).ToNQTimePoint(),
            unitPrice = (long)(unitPrice * 100) // Convert price to the appropriate format
        };

        if (fromMarketContainer)
        {
            marketRequest.source = MarketRequestSource.FROM_MARKET_CONTAINER;
        }

        await RetryHelper.RetryOnExceptionAsync(
        async () =>
        {
            await Mod.bot.Req.MarketPlaceOrder(marketRequest);
            _logger.LogInformation($"Successfully placed market order for {quantity} of item {itemTypeId} in market {marketId} at price {unitPrice}.");
        },
            _botConnectionManager.IsDisconnectedException,
            _botConnectionManager.ReconnectBotAsync
        );
    }

    public async Task<IEnumerable<BuyOrder>> GetBuyOrdersForItem(ulong marketId, ulong itemTypeId)
    {
        return await RetryHelper.RetryOnExceptionAsync(
            async () =>
            {
                var orders = await Mod.bot.Req.MarketSelectItem(new MarketSelectRequest
                {
                    marketIds = new List<ulong> { marketId },
                    itemTypes = new List<ulong> { itemTypeId }
                });

                var buyOrders = orders.orders
                    .Where(order => order.buyQuantity > 0)
                    .Select(order => new BuyOrder
                    {
                        OrderId = order.orderId,
                        ItemId = order.itemType,
                        Quantity = order.buyQuantity,
                        Price = order.unitPrice.amount,
                        MarketId = order.marketId
                    });

                return buyOrders;
            },
            _botConnectionManager.IsDisconnectedException,
            _botConnectionManager.ReconnectBotAsync
        );

    }

    public async Task HandleCraftedItem(ulong itemId, ulong marketId, long quantity)
    {
        _logger.LogInformation($"Handling crafted item: {itemId} {marketId} {quantity}");
        long remainingQuantityToSell = quantity; // Initialize with the total quantity to sell
        var markets = _configService.Config.Market.OperationMarkets; // Get list of markets from config

        // Start with the specified marketId, then move to other markets if needed
        var marketQueue = new Queue<ulong>(markets);
        marketQueue.Enqueue(marketId); // Ensure the passed marketId is processed first

        while (marketQueue.Count > 0 && remainingQuantityToSell > 0)
        {
            var currentMarketId = marketQueue.Dequeue();

            // Retrieve buy orders for the item in the current market
            var buyOrders = await GetBuyOrdersForItem(currentMarketId, itemId);

            // Filter and sort buy orders by price (descending to prioritize the highest price)
            var sortedBuyOrders = buyOrders.OrderByDescending(order => order.Price);

            // Process each buy order until we run out of quantity to sell
            foreach (var buyOrder in sortedBuyOrders)
            {
                if (remainingQuantityToSell <= 0)
                {
                    return;
                }

                var quantityToSell = Math.Min(remainingQuantityToSell, buyOrder.Quantity);

                await CreateItem(itemId, quantityToSell);

                await RetryHelper.RetryOnExceptionAsync(
                    async () =>
                    {
                        // Create the instant market order to sell the items
                        await Mod.bot.Req.MarketInstantOrder(new MarketRequest
                        {
                            marketId = currentMarketId,
                            source = MarketRequestSource.FROM_INVENTORY, // Assuming inventory is the source
                            itemType = itemId,
                            buyQuantity = -quantityToSell, // Negative because we are selling
                            unitPrice = buyOrder.Price,          // Price from the buy order
                            orderId = buyOrder.OrderId           // The buy order we are fulfilling
                        });
                    },
                    _botConnectionManager.IsDisconnectedException,
                    _botConnectionManager.ReconnectBotAsync
                );
                _logger.LogInformation($"Sold {quantityToSell} of item {itemId} at price {buyOrder.Price} in market {currentMarketId}.");

                // Reduce the total quantity to sell
                remainingQuantityToSell -= quantityToSell;
            }
        }

        if (remainingQuantityToSell > 0)
        {
            _logger.LogWarning($"Remaining {remainingQuantityToSell} units of item {itemId} were not sold due to lack of buy orders.");
        }
    }

    public async Task<IEnumerable<SellOrder>> GetSellOrdersForItem(ulong marketId, ulong itemTypeId)
    {
        return await RetryHelper.RetryOnExceptionAsync(
            async () =>
            {
                var orders = await Mod.bot.Req.MarketSelectItem(new MarketSelectRequest
                {
                    marketIds = new List<ulong> { marketId },
                    itemTypes = new List<ulong> { itemTypeId }
                });

                var sellOrders = orders.orders
                    .Where(order => order.buyQuantity < 0) // Negative buyQuantity means sell order
                    .Select(order => new SellOrder
                    {
                        OrderId = order.orderId,
                        ItemId = order.itemType,
                        Quantity = Math.Abs(order.buyQuantity), // Convert negative to positive
                        Price = order.unitPrice.amount,
                        MarketId = order.marketId,
                        OwnerName = order.ownerName,
                        ExpirationDate = order.expirationDate.ToDateTime().DateTime
                    });

                return sellOrders;
            },
            _botConnectionManager.IsDisconnectedException,
            _botConnectionManager.ReconnectBotAsync
        );
    }

    public async Task BuyItemFromSellOrder(SellOrder sellOrder)
    {
        await RetryHelper.RetryOnExceptionAsync(
            async () =>
            {
                await Mod.bot.Req.MarketInstantOrder(new MarketRequest
                {
                    marketId = sellOrder.MarketId,
                    itemType = sellOrder.ItemId,
                    buyQuantity = sellOrder.Quantity,
                    unitPrice = sellOrder.Price,
                    orderId = sellOrder.OrderId
                });
                
                _logger.LogInformation($"Successfully bought {sellOrder.Quantity} of item {sellOrder.ItemId} at price {sellOrder.Price}");
            },
            _botConnectionManager.IsDisconnectedException,
            _botConnectionManager.ReconnectBotAsync
        );
    }

    public async Task<IEnumerable<PurchasedItem>> GetPurchasedItemsFromMarketContainer(ulong marketId)
    {
        return await RetryHelper.RetryOnExceptionAsync(
            async () =>
            {
                var storage = await Mod.bot.Req.MarketContainerGetMyContent(new MarketSelectRequest
                {
                    marketIds = new List<ulong> { marketId },
                    itemTypes = new List<ulong> { _gameplayBank.GetDefinition<NQutils.Def.BaseItem>().Id }
                });

                var purchasedItems = storage.slots
                    .Where(slot => slot.purchased)
                    .Select(slot => new PurchasedItem
                    {
                        ItemId = slot.itemAndQuantity.item.type,
                        Quantity = slot.itemAndQuantity.quantity.value,
                        MarketId = marketId
                    });

                return purchasedItems;
            },
            _botConnectionManager.IsDisconnectedException,
            _botConnectionManager.ReconnectBotAsync
        );
    }
}
