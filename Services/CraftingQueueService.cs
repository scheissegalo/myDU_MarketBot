using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class CraftingQueueService : ICraftingQueueService
{
    private readonly CraftingQueue _craftingQueue;
    private readonly ILogger<CraftingQueueService> _logger;
    private readonly IMarketService _marketService;
    private readonly ConfigService _configService;

    public CraftingQueueService(
        ILogger<CraftingQueueService> logger,
        IMarketService marketService,
        CraftingQueue craftingQueue,
        ConfigService configService
        )
    {
        _craftingQueue = craftingQueue;
        _logger = logger;
        _marketService = marketService;
        _configService = configService;
    }

    public void Start()
    {
        Task.Run(async () => await ProcessQueueAsync());
    }

    private async Task ProcessQueueAsync()
    {
        _logger.LogInformation("Build queue processing started.");

        while (true)
        {
            while (_craftingQueue.Count > 0)
            {
                var currentJob = _craftingQueue.Peek();
                if (DateTime.UtcNow >= currentJob.CraftingStartTime.Add(currentJob.CraftingDuration))
                {
                    await _marketService.HandleCraftedItem(currentJob.ItemId, currentJob.MarketId, currentJob.Quantity);

                    _craftingQueue.Dequeue();
                }

                await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.QueueProcessingTickInSeconds));
            }

            _logger.LogDebug("Finished processing the crafting queue iteration.");
            // Wait before processing the queue again
            await Task.Delay(TimeSpan.FromSeconds(_configService.Config.Market.QueueProcessingTickInSeconds));
        }
    }
}
