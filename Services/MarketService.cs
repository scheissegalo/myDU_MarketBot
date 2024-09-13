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

    public MarketService(
        ILogger<MarketService> logger,
        ConfigService configService,
        IClusterClient clusterClient,
        IGameplayBank gameplayBank,
        IRecipeService recipeService
        )
    {
        _orleans = clusterClient;
        _gameplayBank = gameplayBank;
        _configService = configService;
        _recipeService = recipeService;
        _logger = logger;
    }

    private async Task<bool> HasEnoughMoney(ulong resourceId, double pricePerUnit, long orderSize)
    {
        var wallet = await Mod.bot.Req.GetWallet();
        bool hasEnough = wallet.amount >= (long)(pricePerUnit * orderSize * 100);

        if (!hasEnough)
        {
            _logger.LogWarning($"Not enough money to place order for resource {resourceId}. Needed: {(long)(pricePerUnit * orderSize * 100)}, Available: {wallet.amount}");
        }

        return hasEnough;
    }

    public async Task<MarketOrders> GetMarketOrderForItem(ulong marketId, ulong resourceId)
    {
        var orders = await Mod.bot.Req.MarketGetMyOrders(
                    new MarketSelectRequest
                    {
                        marketIds = new List<ulong> { marketId },
                        itemTypes = new List<ulong> { resourceId }
                    }
                );
        return orders;
    }

    public async Task CreateItem(ulong itemTypeId, long quantity)
    {
        var inventoryGrain = _orleans.GetInventoryGrain(Mod.bot.PlayerId);

        try
        {
            var itemAndQuantity = new ItemAndQuantity
            {
                item = _gameplayBank.GetDefinition(itemTypeId).AsItemInfo(),
                quantity = _gameplayBank.QuantityFromGDValue(itemTypeId, quantity),
            };
            var list = new ItemAndQuantityList();
            list.content.Add(itemAndQuantity);

            await Mod.bot.Req.BotGiveItems(list);

            _logger.LogInformation("Item successfully created in the bot's inventory.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create item in the bots's inventory.");
            throw new InvalidOperationException("Failed to create item in the inventory.", ex);
        }
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

        try
        {
            await Mod.bot.Req.MarketPlaceOrder(marketRequest);
            _logger.LogInformation($"Successfully placed market order for {quantity} of item {itemTypeId} in market {marketId} at price {unitPrice}.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to place market order for {itemTypeId} in market {marketId}. Error: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<BuyOrder>> GetBuyOrdersForItem(ulong marketId, ulong itemTypeId)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to retrieve buy orders for item {itemTypeId} in market {marketId}");
            throw new InvalidOperationException("Error retrieving buy orders.", ex);
        }
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
                    // We have sold everything, stop processing
                    return;
                }

                // Determine how much to sell for this buy order
                var quantityToSell = Math.Min(remainingQuantityToSell, buyOrder.Quantity);
                try
                {
                    // Create only the required number of items to match this buy order
                    await CreateItem(itemId, quantityToSell);

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
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unable to sell {quantityToSell} of item {itemId} at price {buyOrder.Price} in market {currentMarketId}.");
                    throw new InvalidOperationException("Error creating and selling item", ex);
                }
                _logger.LogInformation($"Sold {quantityToSell} of item {itemId} at price {buyOrder.Price} in market {currentMarketId}.");

                // Reduce the total quantity to sell
                remainingQuantityToSell -= quantityToSell;
            }
        }

        // Log a warning if there are any unsold items after processing all markets
        if (remainingQuantityToSell > 0)
        {
            _logger.LogWarning($"Remaining {remainingQuantityToSell} units of item {itemId} were not sold due to lack of buy orders.");
        }
    }
}
