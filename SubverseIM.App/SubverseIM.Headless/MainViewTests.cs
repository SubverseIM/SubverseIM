using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;

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
    public void ShouldStartInContactsView()
    {
        fixture.EnsureWindowShown();

        MainViewModel mainViewModel = fixture.GetViewModel();
        Assert.IsType<ContactPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public void ShouldStartWithNoPreviousView()
    {
        fixture.EnsureWindowShown();

        MainViewModel mainViewModel = fixture.GetViewModel();
        Assert.False(mainViewModel.HasPreviousView);
    }

    [AvaloniaFact]
    public void ShouldNavigateToConfigView() 
    {
        fixture.EnsureWindowShown();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateConfigView();

        Assert.IsType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public void ShouldNavigateToTorrentView()
    {
        fixture.EnsureWindowShown();

        MainViewModel mainViewModel = fixture.GetViewModel();
        mainViewModel.NavigateTorrentView();

        Assert.IsType<TorrentPageViewModel>(mainViewModel.CurrentPage);
    }
}
