using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using System.Security.Cryptography;

namespace SubverseIM.Headless.Tests;

public class MainViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public MainViewTests(MainViewFixture fixture)
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
    
    [AvaloniaFact]
    public async Task ShouldRegisterTopLevelService() 
    {
        await EnsureMainViewLoaded();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        TopLevel? topLevel = serviceManager.Get<TopLevel>();

        Assert.NotNull(topLevel);
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
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToConfigView() 
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        await mainViewModel.NavigateConfigViewAsync();

        Assert.IsType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        await mainViewModel.NavigateContactViewAsync(parentOrNull: null);

        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToCreateContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        await mainViewModel.NavigateContactViewAsync(new SubverseContact 
        { 
            OtherPeer = new(RandomNumberGenerator.GetBytes(20)) 
        });

        Assert.IsType<CreateContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToMessageView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        await mainViewModel.NavigateMessageViewAsync([], null);

        Assert.IsType<MessagePageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToTorrentView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        await mainViewModel.NavigateTorrentViewAsync();

        Assert.IsType<TorrentPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPurchaseView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        await mainViewModel.NavigatePurchaseViewAsync();

        Assert.IsType<PurchasePageViewModel>(mainViewModel.CurrentPage);
    }
}
