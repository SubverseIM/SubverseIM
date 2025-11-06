using Avalonia.Media.Imaging;
using MonoTorrent;
using OpenGraphNet;
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

        public Task<Bitmap?> FetchedBitmapAsync { get; }

        public Task<Stream?> FetchedStreamAsync { get; }

        public EmbedViewModel(IServiceManager serviceManager, string uriString)
        {
            this.serviceManager = serviceManager;
            AbsoluteUri = new Uri(uriString);

            FetchedBitmapAsync = GetBitmapAsync();
            FetchedStreamAsync = GetStreamAsync();
        }

        public async Task<Bitmap?> GetBitmapAsync()
        {
            Stream? stream = await GetStreamAsync();
            if (stream is null) return null;

            try
            {
                return Bitmap.DecodeToWidth(stream, 512);
            }
            catch 
            {
                return null;
            }
        }

        public async Task<Stream?> GetStreamAsync()
        {
            if (MagnetLink.TryParse(AbsoluteUri.OriginalString, out MagnetLink? magnetLink))
            {
                ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
                Progress<TorrentStatus>? progress = await torrentService.StartAsync(
                    new SubverseTorrent(magnetLink.InfoHashes.V1OrV2, AbsoluteUri.OriginalString)
                    );
                if (progress is null) return null;

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

                MemoryStream bufferStream = new();
                using (Stream cacheFileStream = File.OpenRead(cacheFilePath))
                {
                    await cacheFileStream.CopyToAsync(bufferStream);
                }
                bufferStream.Position = 0;
                return bufferStream;
            }
            else
            {
                using HttpClient http = new();

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
                switch (contentType?.ToLowerInvariant()) 
                {
                    case "text/html":
                        OpenGraph og = await OpenGraph.ParseUrlAsync(AbsoluteUri);
                        imageUri = og.Image is null ? null : new Uri(AbsoluteUri.GetLeftPart(UriPartial.Authority) + og.Image.AbsolutePath);
                        break;

                    case "image/png":
                    case "image/jpeg":
                    case "image/webp":
                    case "image/gif":
                        imageUri = AbsoluteUri;
                        break;

                    default:
                        imageUri = null;
                        break;
                }

                if (imageUri is null)
                {
                    return null;
                }
                else
                {
                    MemoryStream bufferStream = new();
                    using (Stream downloadStream = await http.GetStreamAsync(imageUri))
                    {
                        await downloadStream.CopyToAsync(bufferStream);
                    }
                    bufferStream.Position = 0;
                    return bufferStream;
                }
            }
        }
    }
}
