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
        await fixture.InitializeOnceAsync();

        MainView mainView = await fixture.GetViewAsync();
        await mainView.LoadTask;

        return mainView;
    }

    private async Task<(TorrentPageView, TorrentPageViewModel)> EnsureIsOnTorrentPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        await mainViewModel.NavigateTorrentViewAsync();

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

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        await frontendService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        Assert.IsNotType<TorrentPageViewModel>(mainViewModel.CurrentPage);
    }
}
