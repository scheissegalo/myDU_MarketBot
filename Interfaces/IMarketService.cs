using System.Collections.Generic;
using System.Threading.Tasks;
using NQ;

public interface IMarketService
{
    Task CreateItem(ulong itemTypeId, long quantity);
    Task<IEnumerable<BuyOrder>> GetBuyOrdersForItem(ulong marketId, ulong itemId);
    Task HandleCraftedItem(ulong itemId, ulong marketId, long quantity);
}