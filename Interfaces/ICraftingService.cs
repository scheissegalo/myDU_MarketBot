public interface ICraftingService
{
    void CraftItem(CraftingJob job);
    bool CheckResourceAvailability(long itemId);
}