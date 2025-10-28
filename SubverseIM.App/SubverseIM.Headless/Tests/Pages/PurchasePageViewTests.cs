using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class PurchasePageViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public PurchasePageViewTests(MainViewFixture fixture)
    {
        this.fixture = fixture;
    }

    private async Task<MainView> EnsureMainViewLoaded()
    {
        await fixture.InitializeOnceAsync();

        MainView mainView = await fixture.GetViewAsync();
        await mainView.LoadTask;

        return mainView;
    }

    private async Task<(PurchasePageView, PurchasePageViewModel)> EnsureIsOnPurchasePageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        await mainViewModel.NavigatePurchaseViewAsync();

        PurchasePageViewModel? purchasePageViewModel = mainViewModel.CurrentPage as PurchasePageViewModel;
        Assert.NotNull(purchasePageViewModel);

        PurchasePageView? purchasePageView = mainView.GetContentAs<PurchasePageView>();
        Assert.NotNull(purchasePageView);

        await purchasePageView.LoadTask;

        return (purchasePageView, purchasePageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView()
    {
        (PurchasePageView purchasePageView, PurchasePageViewModel purchasePageViewModel) =
            await EnsureIsOnPurchasePageView();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        await frontendService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        Assert.IsNotType<PurchasePageViewModel>(mainViewModel.CurrentPage);
    }
}
