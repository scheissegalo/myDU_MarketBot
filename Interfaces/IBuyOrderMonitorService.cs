public interface IBuyOrderMonitorService
{
    void QueueItemForCrafting(ulong itemId, ulong marketId, ulong orderId, long quantity, int time);
}