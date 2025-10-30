using Avalonia.Platform.Storage;
using MonoTorrent;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface ITorrentService
    {
        Task<IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>>> InitializeAsync();

        Task DestroyAsync();

        Task<bool> AddTorrentAsync(InfoHash infoHash, byte[]? torrentBytes = null);

        Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default);

        Task<bool> RemoveTorrentAsync(SubverseTorrent torrent);

        Task<Progress<TorrentStatus>?> StartAsync(SubverseTorrent torrent);

        Task<bool> StopAsync(SubverseTorrent torrent);
    }
}
