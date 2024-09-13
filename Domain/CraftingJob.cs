using System;

public class CraftingJob
{
    public ulong ItemId { get; set; }
    public ulong MarketId { get; set; }
    public ulong OrderId { get; set; }
    public DateTime CraftingStartTime { get; set; }
    public TimeSpan CraftingDuration { get; set; }
    public long Quantity { get; set; }
}