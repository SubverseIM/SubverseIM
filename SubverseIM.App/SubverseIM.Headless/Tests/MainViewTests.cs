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
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        return mainView;
    }

    [AvaloniaFact]
    public async Task ShouldRegisterTopLevelService() 
    {
        await EnsureMainViewLoaded();

        IServiceManager serviceManager = fixture.GetServiceManager();
        TopLevel? topLevel = serviceManager.Get<TopLevel>();

        Assert.NotNull(topLevel);
    }

    [AvaloniaFact]
    public async Task ShouldRegisterFrontendService()
    {
        await EnsureMainViewLoaded();

        IServiceManager serviceManager = fixture.GetServiceManager();
        IFrontendService? frontendService = serviceManager.Get<IFrontendService>();

        Assert.NotNull(frontendService);
    }

    [AvaloniaFact]
    public async Task ShouldStartInContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToConfigView() 
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateConfigView();

        Assert.IsType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateContactView(parentOrNull: null);

        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToCreateContactView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateContactView(new SubverseContact 
        { 
            OtherPeer = new(RandomNumberGenerator.GetBytes(20)) 
        });

        Assert.IsType<CreateContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToMessageView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateMessageView([], null);

        Assert.IsType<MessagePageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToTorrentView()
    {
        await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateTorrentView();

        Assert.IsType<TorrentPageViewModel>(mainViewModel.CurrentPage);
    }
}
