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
    private readonly ItemIdService _itemIdService;

    // Dictionary to track buy prices for items we've purchased
    private Dictionary<ulong, long> _buyPrices = new Dictionary<ulong, long>();

    public SellOrderMonitorService(
        ILogger<SellOrderMonitorService> logger,
        ConfigService configService,
        IMarketService marketService,
        IRecipeService recipeService,
        BotConnectionManager botConnectionManager,
        ItemIdService itemIdService)
    {
        _logger = logger;
        _configService = configService;
        _marketService = marketService;
        _recipeService = recipeService;
        _botConnectionManager = botConnectionManager;
        _itemIdService = itemIdService;
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
                _logger.LogInformation($"Starting sell order monitoring cycle for markets: {string.Join(", ", _configService.Config.Market.OperationMarkets)}");
                
                var monitoringTasks = _configService.Config.Market.OperationMarkets
                    .Select(marketId => MonitorSellOrders(marketId))
                    .ToList();

                await Task.WhenAll(monitoringTasks);

                _logger.LogInformation($"Completed sell order monitoring cycle. Waiting {_configService.Config.Market.MarketOperationsTickInSeconds} seconds before next cycle.");
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
        try
        {
            _logger.LogInformation($"Monitoring sell orders in market {marketId}");
            var visitedItems = new HashSet<ulong>();

            // Monitor items from all tiers
            for (int tier = 1; tier <= 5; tier++)
            {
                _logger.LogInformation($"Checking tier {tier} in market {marketId}");
                
                var recipes = await _recipeService.GetRecipesByTier(tier);
                _logger.LogInformation($"Found {recipes.Count()} recipes in tier {tier} for market {marketId}");

                int processedItems = 0;
                foreach (var recipe in recipes)
                {
                    foreach (var product in recipe.Products)
                    {
                        if (visitedItems.Contains(product.Id)) continue;

                        visitedItems.Add(product.Id);
                        processedItems++;

                        await ProcessSellOrdersForItem(marketId, product.Id);
                        
                        // Log progress every 100 items to avoid spam
                        if (processedItems % 100 == 0)
                        {
                            _logger.LogInformation($"Processed {processedItems} items in tier {tier} for market {marketId}");
                        }
                    }
                }
                
                _logger.LogInformation($"Completed tier {tier} in market {marketId}, processed {processedItems} unique items");

                // Add a delay between each tier check
                await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.MarketOperationsTickInSeconds));
            }
            
            // Also check for common raw materials and resources that might not be in recipes
            await CheckCommonRawMaterials(marketId);
            
            _logger.LogInformation($"Completed monitoring market {marketId}, checked {visitedItems.Count} unique items");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error monitoring sell orders in market {marketId}");
        }
    }

    private async Task CheckCommonRawMaterials(ulong marketId)
    {
        try
        {
            _logger.LogInformation($"Checking all items from items.yaml in market {marketId}");
            
            // Get all item IDs from the items.yaml file
            var allItemIds = await _itemIdService.GetAllItemIdsAsync();
            
            if (!allItemIds.Any())
            {
                _logger.LogWarning("No item IDs loaded from items.yaml");
                return;
            }

            _logger.LogInformation($"Checking {allItemIds.Count} items from items.yaml in market {marketId}");
            
            int processedCount = 0;
            foreach (var itemId in allItemIds)
            {
                await ProcessSellOrdersForItem(marketId, itemId);
                processedCount++;
                
                // Log progress every 1000 items to avoid spam
                if (processedCount % 1000 == 0)
                {
                    _logger.LogInformation($"Processed {processedCount}/{allItemIds.Count} items from items.yaml in market {marketId}");
                }
            }
            
            _logger.LogInformation($"Completed checking {allItemIds.Count} items from items.yaml in market {marketId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking items from items.yaml in market {marketId}");
        }
    }

    private async Task ProcessSellOrdersForItem(ulong marketId, ulong itemId)
    {
        try
        {
            var sellOrders = await _marketService.GetSellOrdersForItem(marketId, itemId);
            
            if (sellOrders.Any())
            {
                _logger.LogInformation($"Found {sellOrders.Count()} sell orders for item {itemId} in market {marketId}");
            }

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
        _logger.LogDebug($"Checking sell order: Item {sellOrder.ItemId}, Price {sellOrder.Price}, Owner {sellOrder.OwnerName}, Expires {sellOrder.ExpirationDate}");

        // Skip if it's a seeded order (owner is "marketbot" or similar)
        if (sellOrder.OwnerName?.ToLower().Contains("marketbot") == true)
        {
            _logger.LogDebug($"Skipping seeded order from {sellOrder.OwnerName}");
            return false;
        }

        // Skip if it's our own order
        if (sellOrder.OwnerName == _configService.Config.Market.BotName)
        {
            _logger.LogDebug($"Skipping our own order from {sellOrder.OwnerName}");
            return false;
        }

        // Check if price is within our budget
        var maxBuyPrice = _configService.Config.Market.MaxBuyPriceForResell; // Price is already in quantas
        if (sellOrder.Price > maxBuyPrice)
        {
            _logger.LogDebug($"Price {sellOrder.Price} exceeds max buy price {maxBuyPrice}");
            return false;
        }

        // Check if order is close to expiration (within configured days)
        var timeUntilExpiration = sellOrder.ExpirationDate - DateTime.UtcNow;
        if (timeUntilExpiration.TotalDays > _configService.Config.Market.DaysToWaitBeforeExpiration)
        {
            _logger.LogDebug($"Order expires in {timeUntilExpiration.TotalDays:F1} days, but we only buy items expiring within {_configService.Config.Market.DaysToWaitBeforeExpiration} days");
            return false;
        }

        _logger.LogInformation($"✅ Will buy sell order: Item {sellOrder.ItemId}, Price {sellOrder.Price}, Owner {sellOrder.OwnerName}");
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
