﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

public class ModMarketBot : Mod
{
    public ModMarketBot()
    {
    }

    // Main loop for the bot, responsible for initializing and scheduling tasks
    public override async Task Loop()
    {
        var buyOrderMonitorService = serviceProvider.GetRequiredService<BuyOrderMonitorService>();
        buyOrderMonitorService.Start();

        var craftingQueueService = serviceProvider.GetRequiredService<CraftingQueueService>();
        craftingQueueService.Start();

        // Start sell order monitoring if enabled
        var configService = serviceProvider.GetRequiredService<ConfigService>();
        if (configService.Config.Market.EnableBuyAndResell)
        {
            var sellOrderMonitorService = serviceProvider.GetRequiredService<ISellOrderMonitorService>();
            sellOrderMonitorService.Start();
        }

        // Schedule the action to run every 1 minute
        await SafeLoop(Action, 60000, async () =>
        {
            Console.WriteLine("Reconnecting bot...");
            bot = await CreateUser("trader", true, false);
        });
    }

    // Main action logic (for example, mining or other bot tasks)
    public async Task Action()
    {
        await Task.Delay(10000); // Simulate some work
    }
}