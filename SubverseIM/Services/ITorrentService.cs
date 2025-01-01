using Avalonia.Platform.Storage;
using SubverseIM.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface ITorrentService
    {
        Task<bool> AddTorrentAsync(SubverseFile file);

        Task<SubverseFile> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default);

        Task<bool> RemoveTorrentAsync(SubverseFile file);

        Task<Progress<double>?> StartAsync(SubverseFile file);

        Task<bool> StopAsync(SubverseFile file);
    }
}
