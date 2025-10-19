using Plugin.InAppBilling;
using SubverseIM.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class PurchasePageViewModel : PageViewModelBase<PurchasePageViewModel>
    {
        public override bool HasSidebar => false;

        public override bool ShouldConfirmBackNavigation => false;

        public override string Title => "Products View";

        public ObservableCollection<InAppBillingProduct> ProductsList { get; }

        public PurchasePageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            ProductsList = new();
        }

        public async Task InitializeAsync() 
        {
            ProductsList.Clear();

            IBillingService billingService = await ServiceManager.GetWithAwaitAsync<IBillingService>();
            if (await billingService.WasAnyItemPurchasedAsync(["donation_small", "donation_normal", "donation_large"]) == false) 
            {
                foreach (InAppBillingProduct product in await billingService.GetAllProductsAsync())
                {
                    ProductsList.Add(product);
                }
            }
        }

        public async Task PurchaseCommand(string productId) 
        {
            IBillingService billingService = await ServiceManager.GetWithAwaitAsync<IBillingService>();
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();

            if (await billingService.PurchaseItemAsync(productId))
            {
                await frontendService.RestorePurchasesAsync();
                await launcherService.ShowAlertDialogAsync("Thank you!", "Your donation has been processed successfully. Much love!");

                await InitializeAsync();
            }
            else 
            {
                await launcherService.ShowAlertDialogAsync("Oopsie!", "Your donation could not be validated. Please try again soon!");
            }
        }
    }
}
