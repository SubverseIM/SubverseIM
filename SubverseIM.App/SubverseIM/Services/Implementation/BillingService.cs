using Plugin.InAppBilling;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class BillingService : IBillingService
    {
        private static readonly string[] allProductIds = 
        { 
            "donation_small",
            "donation_normal",
            "donation_large",
        };

        public async Task<IEnumerable<InAppBillingProduct>> GetAllProductsAsync() 
        {
            var billing = CrossInAppBilling.Current;
            try
            {
                //You must connect
                var connected = await billing.ConnectAsync();

                if (!connected)
                {
                    return [];
                }

                return await billing.GetProductInfoAsync(ItemType.InAppPurchase, allProductIds);
            }
            finally
            {
                await billing.DisconnectAsync();
            }
        }

        public async Task<bool> WasAnyItemPurchasedAsync(HashSet<string> productIds)
        {
            var billing = CrossInAppBilling.Current;
            try
            {
                var connected = await billing.ConnectAsync();

                if (!connected)
                {
                    //Couldn't connect
                    return false;
                }

                var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);

                //check for null just in case
                if (purchases?.Any(p => productIds.Contains(p.ProductId)) ?? false)
                {
                    //Purchase restored
                    // if on Android may be good to check if these purchases need to be acknowledge
                    return true;
                }
            }
            catch { }
            finally
            {
                await billing.DisconnectAsync();
            }

            return false;
        }

        public async Task<bool> PurchaseItemAsync(string productId)
        {
            var billing = CrossInAppBilling.Current;
            try
            {
                var connected = await billing.ConnectAsync();
                if (!connected)
                {
                    //we are offline or can't connect, don't try to purchase
                    return false;
                }

                //check purchases
                var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);

                //possibility that a null came through.
                if (purchase == null)
                {
                    //did not purchase
                    return false;
                }
                else if (purchase.State == PurchaseState.Purchased)
                {
                    // only need to finalize if on Android unless you turn off auto finalize on iOS
                    var ack = await CrossInAppBilling.Current.FinalizePurchaseAsync([purchase.TransactionIdentifier]);

                    // Handle if acknowledge was successful or not
                    return ack.All(x => x.Success);
                }
            }
            catch { }
            finally
            {
                await billing.DisconnectAsync();
            }

            return false;
        }
    }
}
