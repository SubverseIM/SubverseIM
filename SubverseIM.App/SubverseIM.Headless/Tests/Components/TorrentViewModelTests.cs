using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Headless.XUnit;
using MonoTorrent;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Components;

public class TorrentViewModelTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public TorrentViewModelTests(MainViewFixture fixture)
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
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        mainViewModel.NavigateTorrentView();

        TorrentPageViewModel? torrentPageViewModel = mainViewModel.CurrentPage as TorrentPageViewModel;
        Assert.NotNull(torrentPageViewModel);

        TorrentPageView? torrentPageView = mainView.GetContentAs<TorrentPageView>();
        Assert.NotNull(torrentPageView);

        await torrentPageViewModel.InitializeAsync();

        return (torrentPageView, torrentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldStartTorrent()
    {
        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        TorrentViewModel? torrentViewModel = torrentPageViewModel.Torrents.FirstOrDefault();
        Debug.Assert(torrentViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await torrentViewModel.StartCommand();

        Assert.True(torrentViewModel.IsStarted);
    }

    [AvaloniaFact]
    public async Task ShouldStopTorrent()
    {
        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        TorrentViewModel? torrentViewModel = torrentPageViewModel.Torrents.FirstOrDefault();
        Debug.Assert(torrentViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await torrentViewModel.StopCommand();

        Assert.False(torrentViewModel.IsStarted);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveTorrentFromViewOnDelete()
    {
        IServiceManager serviceManager = fixture.GetServiceManager();
        ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
        
        InfoHash infoHash = new(RandomNumberGenerator.GetBytes(20));
        MagnetLink magnetLink = new(infoHash, name: "UnitTest");
        string checkToken = magnetLink.ToV1String();
        await torrentService.AddTorrentAsync(checkToken);
        
        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        TorrentViewModel? torrentViewModel = torrentPageViewModel.Torrents.FirstOrDefault(x => x.DisplayName == "UnitTest");
        Debug.Assert(torrentViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await torrentViewModel.DeleteCommand();

        Assert.DoesNotContain(torrentViewModel, torrentPageViewModel.Torrents);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveTorrentFromDatabaseOnDelete()
    {
        IServiceManager serviceManager = fixture.GetServiceManager();
        ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();

        InfoHash infoHash = new(RandomNumberGenerator.GetBytes(20));
        MagnetLink magnetLink = new(infoHash, name: "UnitTest");
        string checkToken = magnetLink.ToV1String();
        await torrentService.AddTorrentAsync(checkToken);

        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        TorrentViewModel? torrentViewModel = torrentPageViewModel.Torrents.FirstOrDefault(x => x.DisplayName == "UnitTest");
        Debug.Assert(torrentViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await torrentViewModel.DeleteCommand();
        
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        Assert.DoesNotContain(checkToken, (await dbService.GetTorrentsAsync()).Select(x => x.MagnetUri));
    }

    [AvaloniaFact]
    public async Task ShouldThrowExceptionOnExportEmpty() 
    {
        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        TorrentViewModel? torrentViewModel = torrentPageViewModel.Torrents.FirstOrDefault();
        Debug.Assert(torrentViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await Assert.ThrowsAsync<InvalidOperationException>(() => torrentViewModel.ExportCommand(null));
    }
}
