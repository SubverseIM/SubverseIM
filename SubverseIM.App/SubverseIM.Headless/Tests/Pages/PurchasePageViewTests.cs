using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class PurchasePageViewTests
{
    private readonly MainViewFixture fixture;

    public PurchasePageViewTests()
    {
        fixture = new MainViewFixture();
    }

    private async Task<MainView> EnsureMainViewLoaded()
    {
        await fixture.InitializeAsync();

        MainView mainView = await fixture.GetViewAsync();
        await mainView.LoadTask;

        return mainView;
    }

    private async Task<(PurchasePageView, PurchasePageViewModel)> EnsureIsOnPurchasePageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        while (await navService.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        await navService.NavigatePurchaseViewAsync();

        PurchasePageView? purchasePageView = mainView.GetContentAs<PurchasePageView>();
        Assert.NotNull(purchasePageView);

        PurchasePageViewModel? purchasePageViewModel = purchasePageView.DataContext as PurchasePageViewModel;
        Assert.NotNull(purchasePageViewModel);

        await purchasePageView.LoadTask;

        return (purchasePageView, purchasePageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView()
    {
        (PurchasePageView purchasePageView, PurchasePageViewModel purchasePageViewModel) =
            await EnsureIsOnPurchasePageView();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        INavigationService navService = await serviceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsNotType<PurchasePageViewModel>(mainView.CurrentPage);
    }
}
