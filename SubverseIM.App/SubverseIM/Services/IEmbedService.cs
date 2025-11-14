using SixLabors.ImageSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IEmbedService
    {
        Task<Image> GetCacheImageAsync(FileInfo imageFileInfo, CancellationToken cancellationToken = default);

        Task<Image> GetCacheImageAsync(Uri imageUri, CancellationToken cancellationToken = default);
    }
}
