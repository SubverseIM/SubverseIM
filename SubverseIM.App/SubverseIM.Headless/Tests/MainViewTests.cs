using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;
using System.Security.Cryptography;

namespace SubverseIM.Headless.Tests;

public class MainViewTests
{
    private readonly MainViewFixture fixture;

    public MainViewTests()
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

    [AvaloniaFact]
    public async Task ShouldRegisterFrontendService()
    {
        await EnsureMainViewLoaded();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IFrontendService? frontendService = serviceManager.Get<IFrontendService>();

        Assert.NotNull(frontendService);
    }

    [AvaloniaFact]
    public async Task ShouldStartInContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        while (await navService.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<ContactPageView>(mainView.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToConfigView() 
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigateConfigViewAsync();

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<ConfigPageView>(mainView.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigateContactViewAsync(parentOrNull: null);

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<ContactPageView>(mainView.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToCreateContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigateContactViewAsync(new SubverseContact 
        { 
            OtherPeer = new(RandomNumberGenerator.GetBytes(20)) 
        });

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<CreateContactPageView>(mainView.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToMessageView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigateMessageViewAsync([], null);

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<MessagePageView>(mainView.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToTorrentView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigateTorrentViewAsync();

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<TorrentPageView>(mainView.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPurchaseView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigatePurchaseViewAsync();

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsType<PurchasePageView>(mainView.CurrentPage);
    }
}
