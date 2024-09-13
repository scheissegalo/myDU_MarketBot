using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class BuyOrderMonitorService : IBuyOrderMonitorService
{
    private readonly ILogger<BuyOrderMonitorService> _logger;
    private readonly ConfigService _configService;
    private readonly CraftingQueue _craftingQueue;
    private readonly IMarketService _marketService;
    private readonly IRecipeService _recipeService;

    // Constructor injection of dependencies
    public BuyOrderMonitorService(
        ILogger<BuyOrderMonitorService> logger,
        ConfigService configService,
        CraftingQueue craftingQueue,
        IMarketService marketService,
        IRecipeService recipeService)
    {
        _logger = logger;
        _configService = configService;
        _craftingQueue = craftingQueue;
        _marketService = marketService;
        _recipeService = recipeService;

    }

    public void Start()
    {
        Task.Run(async () => await StartBuyOrderMonitoringLoop());
    }

    private async Task StartBuyOrderMonitoringLoop()
    {
        _logger.LogInformation("Buy order monitoring started.");

        while (true)
        {
            try
            {
                var monitoringTasks = _configService.Config.Market.OperationMarkets
                    .Select(marketId => MonitorBuyOrders(marketId))
                    .ToList();

                await Task.WhenAll(monitoringTasks);

                await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.MarketOperationsTickInSeconds));
            }
            catch (OperationCanceledException)
            {
                // Operation was canceled, handle it here if needed
                _logger.LogInformation("Buy order monitoring was canceled.");
                break;
            }
            catch (Exception ex)
            {
                // Handle any other errors
                _logger.LogError(ex, "Error during buy order monitoring loop.");
            }
        }

        _logger.LogInformation("Buy order monitoring loop exited.");
    }

    private async Task MonitorBuyOrders(ulong marketId)
    {
        var visitedItems = new HashSet<ulong>();

        for (int tier = 1; tier <= 5; tier++)
        {
            var recipes = await _recipeService.GetRecipesByTier(tier);

            foreach (var recipe in recipes)
            {
                foreach (var product in recipe.Products)
                {
                    if (visitedItems.Contains(product.Id)) continue;

                    visitedItems.Add(product.Id);

                    var buyOrders = await _marketService.GetBuyOrdersForItem(marketId, product.Id);

                    foreach (var buyOrder in buyOrders)
                    {
                        if (ShouldCraft(buyOrder) && !_craftingQueue.itemQueued(buyOrder.ItemId))
                        {
                            QueueItemForCrafting(buyOrder.ItemId, marketId, buyOrder.OrderId, buyOrder.Quantity, recipe.Time);
                        }
                    }
                }
            }

            // Add a delay between each tier check
            await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.MarketOperationsTickInSeconds));
        }
    }

    public void QueueItemForCrafting(ulong itemId, ulong marketId, ulong orderId, long quantity, int time)
    {
        var craftingJob = new CraftingJob
        {
            ItemId = itemId,
            MarketId = marketId,
            OrderId = orderId,
            CraftingStartTime = DateTime.UtcNow,
            CraftingDuration = TimeSpan.FromSeconds(time),
            Quantity = quantity
        };

        _craftingQueue.Add(craftingJob);
    }


    private bool ShouldCraft(BuyOrder buyOrder)
    {
        return buyOrder.Price > 100*100; // Example threshold. *100 is to adjust format
    }
}
