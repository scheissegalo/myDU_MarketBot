using System;

public class SellOrder
{
    public ulong OrderId { get; set; }
    public ulong ItemId { get; set; }
    public long Quantity { get; set; }
    public long Price { get; set; }
    public ulong MarketId { get; set; }
    public string OwnerName { get; set; }
    public DateTime ExpirationDate { get; set; }
}
