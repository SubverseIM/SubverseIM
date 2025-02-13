using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class TorrentPageViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public TorrentPageViewTests(MainViewFixture fixture)
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

    private async Task<(TorrentPageView, TorrentPageViewModel)> EnsureIsOnTorrentPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        mainViewModel.NavigateTorrentView();

        TorrentPageViewModel? torrentPageViewModel = mainViewModel.CurrentPage as TorrentPageViewModel;
        Assert.NotNull(torrentPageViewModel);

        TorrentPageView? torrentPageView = mainView.GetContentAs<TorrentPageView>();
        Assert.NotNull(torrentPageView);

        await torrentPageView.LoadTask;

        return (torrentPageView, torrentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldLoadTorrents()
    {
        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        Assert.Equal(
            MainViewFixture.EXPECTED_NUM_TORRENTS, 
            torrentPageViewModel.Torrents.Count
            );
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView()
    {
        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        IServiceManager serviceManager = fixture.GetServiceManager();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        frontendService.NavigatePreviousView();

        MainViewModel mainViewModel = fixture.GetViewModel();
        Assert.IsNotType<TorrentPageViewModel>(mainViewModel.CurrentPage);
    }
}
