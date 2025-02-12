using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IBillingService
    {
        Task<bool> WasItemPurchasedAsync(string productId);

        Task<bool> PurchaseItemAsync(string productId);
    }
}
