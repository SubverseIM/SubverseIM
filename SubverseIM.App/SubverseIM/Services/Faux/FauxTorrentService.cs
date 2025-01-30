using Avalonia.Platform.Storage;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class FauxTorrentService : ITorrentService
    {
        private readonly IServiceManager serviceManager;

        public FauxTorrentService(IServiceManager serviceManager) 
        {
            this.serviceManager = serviceManager;
        }

        public Task<IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>>> InitializeAsync()
        {
            return Task.FromResult<IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>>>(
                new Dictionary<SubverseTorrent, Progress<TorrentStatus>>()
                );
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public Task<bool> AddTorrentAsync(string magnetUri)
        {
            return Task.FromResult(false);
        }

        public Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SubverseTorrent(string.Empty));
        }

        public Task<bool> RemoveTorrentAsync(SubverseTorrent torrent)
        {
            return Task.FromResult(false);
        }

        public Task<Progress<TorrentStatus>?> StartAsync(SubverseTorrent torrent)
        {
            return Task.FromResult<Progress<TorrentStatus>?>(null);
        }

        public Task<bool> StopAsync(SubverseTorrent torrent)
        {
            return Task.FromResult(false);
        }
    }
}