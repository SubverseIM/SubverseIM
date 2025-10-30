using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Headless.XUnit;
using MonoTorrent;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
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
        await fixture.InitializeOnceAsync();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
        
        InfoHash infoHash = new(RandomNumberGenerator.GetBytes(20));
        MagnetLink magnetLink = new(infoHash, name: "UnitTest");
        await dbService.InsertOrUpdateItemAsync(new SubverseTorrent(infoHash, magnetLink.ToV1String()));
        await torrentService.AddTorrentAsync(infoHash);
        
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
        await fixture.InitializeOnceAsync();
        
        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();

        InfoHash infoHash = new(RandomNumberGenerator.GetBytes(20));
        MagnetLink magnetLink = new(infoHash, name: "UnitTest");
        await dbService.InsertOrUpdateItemAsync(new SubverseTorrent(infoHash, magnetLink.ToV1String()));
        await torrentService.AddTorrentAsync(infoHash);

        (TorrentPageView torrentPageView, TorrentPageViewModel torrentPageViewModel) =
            await EnsureIsOnTorrentPageView();

        TorrentViewModel? torrentViewModel = torrentPageViewModel.Torrents.FirstOrDefault(x => x.DisplayName == "UnitTest");
        Debug.Assert(torrentViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await torrentViewModel.DeleteCommand();
        Assert.DoesNotContain(infoHash, (await dbService.GetTorrentsAsync()).Select(x => x.InfoHash));
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
