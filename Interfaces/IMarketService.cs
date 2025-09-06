using System.Collections.Generic;
using System.Threading.Tasks;
using NQ;

public interface IMarketService
{
    Task CreateItem(ulong itemTypeId, long quantity);
    Task<IEnumerable<BuyOrder>> GetBuyOrdersForItem(ulong marketId, ulong itemId);
    Task HandleCraftedItem(ulong itemId, ulong marketId, long quantity);
    Task<IEnumerable<SellOrder>> GetSellOrdersForItem(ulong marketId, ulong itemId);
    Task BuyItemFromSellOrder(SellOrder sellOrder);
    Task<IEnumerable<PurchasedItem>> GetPurchasedItemsFromMarketContainer(ulong marketId);
    Task PlaceMarketOrder(ulong marketId, ulong itemTypeId, long quantity, double unitPrice, bool sell = false, bool fromMarketContainer = false);
}