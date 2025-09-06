using System;
using System.Collections.Generic;

public class MarketBotConfig
{
    public MarketSettings Market { get; set; } = new MarketSettings();
}

public class MarketSettings
{
    public List<ulong> OperationMarkets { get; set; }
    public int MarketOperationsTickInSeconds { get; set; }
    public int QueueProcessingTickInSeconds { get; set; }
    public int MinimumBuyOrderPrice { get; set; } = 100; // Default value in quantas
    public bool EnableBuyAndResell { get; set; } = false;
    public int MaxBuyPriceForResell { get; set; } = 1000; // Maximum price to buy items for reselling (in quantas)
    public double ResellMarkup { get; set; } = 1.1; // 10% markup (1.1 = 110%)
    public int DaysToWaitBeforeExpiration { get; set; } = 1; // Only buy orders expiring within this many days
    public string BotName { get; set; } = "MarketBot";
}