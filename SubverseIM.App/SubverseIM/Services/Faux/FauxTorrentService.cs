using Avalonia.Platform.Storage;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Tests
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
            throw new NotImplementedException();
        }

        public Task DestroyAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> AddTorrentAsync(string magnetUri)
        {
            throw new NotImplementedException();
        }

        public Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveTorrentAsync(SubverseTorrent torrent)
        {
            throw new NotImplementedException();
        }

        public Task<Progress<TorrentStatus>?> StartAsync(SubverseTorrent torrent)
        {
            throw new NotImplementedException();
        }

        public Task<bool> StopAsync(SubverseTorrent torrent)
        {
            throw new NotImplementedException();
        }
    }
}