
using SubverseIM.Core.Storage.Blobs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services;

public interface IBlobService
{
    Task<IBlobStore<FileInfo>> GetFileStoreAsync(Uri hostAddress, CancellationToken cancellationToken = default);

    Task<IBlobSource<FileInfo>> GetFileSourceAsync(string filePath, CancellationToken cancellationToken = default);
}