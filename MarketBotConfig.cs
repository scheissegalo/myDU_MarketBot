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
}