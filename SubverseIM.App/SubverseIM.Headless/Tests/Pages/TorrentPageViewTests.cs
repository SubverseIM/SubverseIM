using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class TorrentPageViewTests
{
    private readonly MainViewFixture fixture;

    public TorrentPageViewTests()
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

    private async Task<(TorrentPageView, TorrentPageViewModel)> EnsureIsOnTorrentPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        while (await navService.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        await navService.NavigateTorrentViewAsync();

        TorrentPageView? torrentPageView = mainView.GetContentAs<TorrentPageView>();
        Assert.NotNull(torrentPageView);

        TorrentPageViewModel? torrentPageViewModel = torrentPageView.DataContext as TorrentPageViewModel;
        Assert.NotNull(torrentPageViewModel);

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
        INavigationService navService = await serviceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsNotType<TorrentPageView>(mainView.CurrentPage);
    }
}
