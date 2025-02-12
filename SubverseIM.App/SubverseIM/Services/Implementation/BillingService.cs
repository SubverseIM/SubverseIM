using Plugin.InAppBilling;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class BillingService : IBillingService
    {
        public async Task<bool> WasItemPurchasedAsync(string productId)
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
                if (purchases?.Any(p => p.ProductId == productId) ?? false)
                {
                    //Purchase restored
                    // if on Android may be good to check if these purchases need to be acknowledge
                    return true;
                }
                else
                {
                    //no purchases found
                    return false;
                }
            }
            finally
            {
                await billing.DisconnectAsync();
            }
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
                    return true;
                }
            }
            finally
            {
                await billing.DisconnectAsync();
            }

            return false;
        }
    }
}
