using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class SellOrderMonitorService : ISellOrderMonitorService
{
    private readonly ILogger<SellOrderMonitorService> _logger;
    private readonly ConfigService _configService;
    private readonly IMarketService _marketService;
    private readonly IRecipeService _recipeService;
    private readonly BotConnectionManager _botConnectionManager;

    // Dictionary to track buy prices for items we've purchased
    private Dictionary<ulong, long> _buyPrices = new Dictionary<ulong, long>();

    public SellOrderMonitorService(
        ILogger<SellOrderMonitorService> logger,
        ConfigService configService,
        IMarketService marketService,
        IRecipeService recipeService,
        BotConnectionManager botConnectionManager)
    {
        _logger = logger;
        _configService = configService;
        _marketService = marketService;
        _recipeService = recipeService;
        _botConnectionManager = botConnectionManager;
    }

    public void Start()
    {
        Task.Run(async () => await StartSellOrderMonitoringLoop());
        Task.Run(async () => await StartPurchasedItemsProcessingLoop());
    }

    private async Task StartSellOrderMonitoringLoop()
    {
        _logger.LogInformation("Sell order monitoring started.");

        while (true)
        {
            try
            {
                var monitoringTasks = _configService.Config.Market.OperationMarkets
                    .Select(marketId => MonitorSellOrders(marketId))
                    .ToList();

                await Task.WhenAll(monitoringTasks);

                await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.MarketOperationsTickInSeconds));
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sell order monitoring was canceled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sell order monitoring loop.");
            }
        }

        _logger.LogInformation("Sell order monitoring loop exited.");
    }

    private async Task StartPurchasedItemsProcessingLoop()
    {
        _logger.LogInformation("Purchased items processing started.");

        while (true)
        {
            try
            {
                var processingTasks = _configService.Config.Market.OperationMarkets
                    .Select(marketId => ProcessPurchasedItems(marketId))
                    .ToList();

                await Task.WhenAll(processingTasks);

                await Task.Delay(TimeSpan.FromSeconds(30)); // Check every 30 seconds for purchased items
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Purchased items processing was canceled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during purchased items processing loop.");
            }
        }

        _logger.LogInformation("Purchased items processing loop exited.");
    }

    private async Task MonitorSellOrders(ulong marketId)
    {
        var visitedItems = new HashSet<ulong>();

        // Monitor items from all tiers
        for (int tier = 1; tier <= 5; tier++)
        {
            var recipes = await _recipeService.GetRecipesByTier(tier);

            foreach (var recipe in recipes)
            {
                foreach (var product in recipe.Products)
                {
                    if (visitedItems.Contains(product.Id)) continue;

                    visitedItems.Add(product.Id);

                    await ProcessSellOrdersForItem(marketId, product.Id);
                }
            }

            // Add a delay between each tier check
            await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.MarketOperationsTickInSeconds));
        }
    }

    private async Task ProcessSellOrdersForItem(ulong marketId, ulong itemId)
    {
        try
        {
            var sellOrders = await _marketService.GetSellOrdersForItem(marketId, itemId);

            foreach (var sellOrder in sellOrders)
            {
                if (ShouldBuySellOrder(sellOrder))
                {
                    await BuyAndResellItem(sellOrder);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing sell orders for item {itemId} in market {marketId}");
        }
    }

    private bool ShouldBuySellOrder(SellOrder sellOrder)
    {
        // Skip if it's a seeded order (owner is "marketbot" or similar)
        if (sellOrder.OwnerName?.ToLower().Contains("marketbot") == true)
        {
            return false;
        }

        // Skip if it's our own order
        if (sellOrder.OwnerName == _configService.Config.Market.BotName)
        {
            return false;
        }

        // Check if price is within our budget
        var maxBuyPrice = _configService.Config.Market.MaxBuyPriceForResell * 100; // Convert to internal format
        if (sellOrder.Price > maxBuyPrice)
        {
            return false;
        }

        // Check if order is close to expiration (within configured days)
        var timeUntilExpiration = sellOrder.ExpirationDate - DateTime.UtcNow;
        if (timeUntilExpiration.TotalDays > _configService.Config.Market.DaysToWaitBeforeExpiration)
        {
            return false;
        }

        return true;
    }

    private async Task BuyAndResellItem(SellOrder sellOrder)
    {
        try
        {
            _logger.LogInformation($"Buying {sellOrder.Quantity} of item {sellOrder.ItemId} at price {sellOrder.Price} from {sellOrder.OwnerName}");

            // Buy the item
            await _marketService.BuyItemFromSellOrder(sellOrder);

            // Store the buy price for reselling
            _buyPrices[sellOrder.ItemId] = sellOrder.Price;

            _logger.LogInformation($"Successfully bought item {sellOrder.ItemId}. Will resell at 10% markup.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to buy item {sellOrder.ItemId} from sell order {sellOrder.OrderId}");
        }
    }

    public async Task ProcessPurchasedItems(ulong marketId)
    {
        try
        {
            var purchasedItems = await _marketService.GetPurchasedItemsFromMarketContainer(marketId);

            foreach (var item in purchasedItems)
            {
                if (_buyPrices.TryGetValue(item.ItemId, out long buyPrice))
                {
                    var resellPrice = (long)(buyPrice * _configService.Config.Market.ResellMarkup);
                    
                    _logger.LogInformation($"Reselling {item.Quantity} of item {item.ItemId} at price {resellPrice} (bought at {buyPrice})");

                    await _marketService.PlaceMarketOrder(
                        marketId, 
                        item.ItemId, 
                        item.Quantity, 
                        resellPrice / 100.0, // Convert back to display format
                        sell: true, 
                        fromMarketContainer: true
                    );

                    // Remove from tracking after successful listing
                    _buyPrices.Remove(item.ItemId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing purchased items in market {marketId}");
        }
    }
}
