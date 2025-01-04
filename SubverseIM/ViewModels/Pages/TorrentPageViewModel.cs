using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class TorrentPageViewModel : PageViewModelBase
    {
        public override string Title => "File Manager";

        public override bool HasSidebar => false;

        public ObservableCollection<TorrentViewModel> Torrents { get; }

        public TorrentPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            Torrents = new();
        }

        public async Task InitializeAsync(Uri? launchedUri = null, bool unique = true)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>();

            IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>> torrents = await torrentService.InitializeAsync();
            foreach ((SubverseTorrent torrent, Progress<TorrentStatus> status) in torrents) 
            {
                Torrents.Add(new(this, torrent, status));
            }

            SubverseTorrent? torrentToAdd = launchedUri is null ? null : new(launchedUri.ToString());
            if (torrentToAdd is not null && await torrentService.AddTorrentAsync(torrentToAdd))
            {
                dbService.InsertOrUpdateItem(torrentToAdd);
                Torrents.Add(new(this, torrentToAdd, null));
            }
            else if (torrentToAdd is not null && !unique)
            {
                Torrents.Add(new(this, torrentToAdd, null));
            }
        }

        public async Task DeinitializeAsync() 
        {
            ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>();
            await torrentService.DeinitializeAsync();
        }
    }
}
