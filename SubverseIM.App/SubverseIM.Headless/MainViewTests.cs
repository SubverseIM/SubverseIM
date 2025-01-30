using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using System.Security.Cryptography;

namespace SubverseIM.Headless;

public class MainViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public MainViewTests(MainViewFixture fixture)
    {
        this.fixture = fixture;
    }

    [AvaloniaFact]
    public async Task ShouldRegisterTopLevelService() 
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        IServiceManager serviceManager = fixture.GetServiceManager();
        TopLevel? topLevel = serviceManager.Get<TopLevel>();

        Assert.NotNull(topLevel);
    }

    [AvaloniaFact]
    public async Task ShouldRegisterFrontendService()
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        IServiceManager serviceManager = fixture.GetServiceManager();
        IFrontendService? frontendService = serviceManager.Get<IFrontendService>();

        Assert.NotNull(frontendService);
    }

    [AvaloniaFact]
    public async Task ShouldStartInContactView()
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToConfigView() 
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateConfigView();

        Assert.IsType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToContactView()
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateContactView(parentOrNull: null);

        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToCreateContactView()
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

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
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateMessageView([], null);

        Assert.IsType<MessagePageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToTorrentView()
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateTorrentView();

        Assert.IsType<TorrentPageViewModel>(mainViewModel.CurrentPage);
    }
}
