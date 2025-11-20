using Avalonia.Threading;
using CFS.Surge.Core;
using MonoTorrent;
using OpenGraphNet;
using ReactiveUI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SubverseIM.ViewModels.Components
{
    public class EmbedViewModel : ViewModelBase
    {
        private readonly IServiceManager serviceManager;

        public Uri AbsoluteUri { get; }

        public string DisplayName => AbsoluteUri.Scheme switch
        {
            "magnet" => MagnetLink.TryParse(AbsoluteUri.OriginalString, out MagnetLink? magnetLink) ?
                $"Attachment: {magnetLink.Name ?? "Untitled"} ({UnitHelpers.ByteCountToString(magnetLink.Size)})" : null,
            "sv" => "Contact: " + (HttpUtility.ParseQueryString(AbsoluteUri.Query)["name"] ?? "Anonymous"),
            "http" or "https" => "Link: " + AbsoluteUri.Host,
            _ => null
        } ?? AbsoluteUri.ToString();


        private Image? fetchedImage;
        public Image? FetchedImage
        {
            get => fetchedImage;
            private set => this.RaiseAndSetIfChanged(ref fetchedImage, value);
        }

        public EmbedViewModel(IServiceManager serviceManager, string uriString)
        {
            this.serviceManager = serviceManager;
            AbsoluteUri = new Uri(uriString);

            _ = Task.Run(LoadImageAsync);
        }

        private async Task LoadImageAsync()
        {
            if (MagnetLink.TryParse(AbsoluteUri.OriginalString, out MagnetLink? magnetLink))
            {
                ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
                Progress<TorrentStatus>? progress = await torrentService.StartAsync(
                    new SubverseTorrent(magnetLink.InfoHashes.V1OrV2, AbsoluteUri.OriginalString)
                    );
                if (progress is null) return;

                TaskCompletionSource tcs = new();
                progress.ProgressChanged += (s, ev) =>
                {
                    if (ev.Complete) tcs.TrySetResult();
                };
                await tcs.Task;

                ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
                string cacheDirPath = Path.Combine(
                        launcherService.GetPersistentStoragePath(), "torrent", "files",
                        magnetLink.InfoHashes.V1OrV2.ToHex()
                        );
                string cacheFilePath = Path.Combine(cacheDirPath,
                    magnetLink.Name ?? throw new InvalidOperationException("No display name was provided for this file!")
                    );

                IEmbedService embedService = await serviceManager.GetWithAwaitAsync<IEmbedService>();
                Image? fetchedImage = await embedService.GetCacheImageAsync(new FileInfo(cacheFilePath));
                await Dispatcher.UIThread.InvokeAsync(() => 
                {
                    Image? previouslyFetchedImage = FetchedImage;
                    FetchedImage = fetchedImage; 
                    previouslyFetchedImage?.Dispose();
                });
            }
            else
            {
                HttpClient http = await serviceManager.GetWithAwaitAsync<HttpClient>();

                string? contentType;
                try
                {
                    using (HttpRequestMessage headRequest = new(HttpMethod.Head, AbsoluteUri))
                    using (HttpResponseMessage headResponse = await http.SendAsync(headRequest))
                    {
                        headResponse.EnsureSuccessStatusCode();
                        contentType = headResponse.Content.Headers.ContentType?.MediaType;
                    }
                }
                catch (HttpRequestException)
                {
                    contentType = null;
                }

                Uri? imageUri;
                bool isProgressive;
                switch (contentType?.ToLowerInvariant())
                {
                    case "text/html":
                        OpenGraph og = await OpenGraph.ParseUrlAsync(AbsoluteUri);
                        imageUri = og.Image is null ? null : new Uri(AbsoluteUri.GetLeftPart(UriPartial.Authority) + og.Image.AbsolutePath);
                        isProgressive = false;
                        break;

                    case "image/png":
                    case "image/jpeg":
                    case "image/webp":
                    case "image/gif":
                        imageUri = AbsoluteUri;
                        isProgressive = false;
                        break;

                    case "application/x-cfs-surge":
                        imageUri = AbsoluteUri;
                        isProgressive = true;
                        break;

                    default:
                        imageUri = null;
                        isProgressive = false;
                        break;
                }

                if (imageUri is null)
                {
                    return;
                }
                else if (isProgressive)
                {
                    HttpClient httpClient = await serviceManager.GetWithAwaitAsync<HttpClient>();
                    using Stream imageUriStream = await httpClient.GetStreamAsync(imageUri);
                    if (SurgeStreamDecoder.TryCreateDecoder(imageUriStream, null, true,
                        out SurgeStreamDecoder? imageUriDecoder))
                    {
                        using (imageUriDecoder)
                        {
                            await foreach (Image<Bgra32> fetchedImage in imageUriDecoder.DecodeAsync<Bgra32>())
                            {
                                Image? previouslyFetchedImage = FetchedImage;
                                await Dispatcher.UIThread.InvokeAsync(
                                    () => FetchedImage = fetchedImage
                                    );
                                previouslyFetchedImage?.Dispose();
                            }
                        }
                    }
                }
                else
                {
                    IEmbedService embedService = await serviceManager.GetWithAwaitAsync<IEmbedService>();
                    Image fetchedImage = await embedService.GetCacheImageAsync(imageUri);
                    await Dispatcher.UIThread.InvokeAsync(
                        () => FetchedImage = fetchedImage
                        );
                }
            }
        }
    }
}
