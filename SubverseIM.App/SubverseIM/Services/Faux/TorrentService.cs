using Avalonia.Platform.Storage;
using SubverseIM.Models;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class TorrentService : ITorrentService
    {
        private readonly IServiceManager serviceManager;

        public TorrentService(IServiceManager serviceManager)
        {
            this.serviceManager = serviceManager;
        }

        public async Task<IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>>> InitializeAsync()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            return dbService.GetTorrents().ToFrozenDictionary(
                x => x, x => new Progress<TorrentStatus>()
                );
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<bool> AddTorrentAsync(string magnetUri)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            return !dbService.InsertOrUpdateItem(new SubverseTorrent(magnetUri));
        }

        public Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            return Task.FromException<SubverseTorrent>(new PlatformNotSupportedException());
        }

        public async Task<bool> RemoveTorrentAsync(SubverseTorrent torrent)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubverseTorrent? storedTorrent = dbService.GetTorrent(torrent.MagnetUri);
            return storedTorrent is not null && dbService.DeleteItemById<SubverseTorrent>(storedTorrent.Id);
        }

        public Task<Progress<TorrentStatus>?> StartAsync(SubverseTorrent torrent)
        {
            return Task.FromResult<Progress<TorrentStatus>?>(new Progress<TorrentStatus>());
        }

        public Task<bool> StopAsync(SubverseTorrent torrent)
        {
            return Task.FromResult(true);
        }
    }
}