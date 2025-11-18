using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class EmbedService : IEmbedService
    {
        private readonly IServiceManager serviceManager;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<Image>> cachedImageMap;

        public EmbedService(IServiceManager serviceManager)
        {
            this.serviceManager = serviceManager;
            cachedImageMap = new();
        }

        public async Task<Image> GetCacheImageAsync(FileInfo imageFileInfo, CancellationToken cancellationToken)
        {
            string imageFilePath = imageFileInfo.FullName;

            bool addedFlag = false;
            TaskCompletionSource<Image> imageTcs = cachedImageMap.GetOrAdd(
                imageFilePath, x => { addedFlag = true; return new(); });

            if (addedFlag)
            {
                try
                {
                    using Stream imageFileStream = imageFileInfo.OpenRead();
                    Image image = await Image.LoadAsync<Bgra32>(imageFileStream, cancellationToken);
                    imageTcs.SetResult(image);
                }
                catch (OperationCanceledException)
                {
                    imageTcs.SetCanceled(cancellationToken);
                }
                catch (Exception ex) 
                {
                    imageTcs.SetException(ex);
                }
            }

            return await imageTcs.Task;
        }

        public async Task<Image> GetCacheImageAsync(Uri imageUri, CancellationToken cancellationToken)
        {
            string imageUriStr = imageUri.OriginalString;

            bool addedFlag = false;
            TaskCompletionSource<Image> imageTcs = cachedImageMap.GetOrAdd(
                imageUriStr, x => { addedFlag = true; return new(); });

            if (addedFlag)
            {
                try
                {
                    HttpClient httpClient = await serviceManager.GetWithAwaitAsync<HttpClient>();
                    using Stream imageUriStream = await httpClient.GetStreamAsync(imageUri, cancellationToken);
                    Image image = await Image.LoadAsync<Bgra32>(imageUriStream, cancellationToken);
                    imageTcs.SetResult(image);
                }
                catch (OperationCanceledException)
                {
                    imageTcs.SetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    imageTcs.SetException(ex);
                }
            }

            return await imageTcs.Task;
        }
    }
}
