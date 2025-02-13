using Plugin.InAppBilling;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IBillingService
    {
        Task<IEnumerable<InAppBillingProduct>> GetAllProductsAsync();

        Task<bool> WasAnyItemPurchasedAsync(HashSet<string> productIds);

        Task<bool> PurchaseItemAsync(string productId);
    }
}
