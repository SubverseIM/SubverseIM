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

        public EmbedViewModel(IServiceManager serviceManager, string uriString)
        {
            this.serviceManager = serviceManager;
            AbsoluteUri = new Uri(uriString);

            FetchedBitmapAsync = GetBitmapAsync();
        }

        public async Task<Bitmap?> GetBitmapAsync()
        {
            OpenGraph og;
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
                using Stream stream = File.OpenRead(cacheFilePath);
                return Bitmap.DecodeToWidth(stream, 256);
            }
            else if ((og = await OpenGraph.ParseUrlAsync(AbsoluteUri)).Image is not null)
            {
                using MemoryStream bufferStream = new();
                using (HttpClient http = new())
                using (Stream downloadStream = await http.GetStreamAsync($"{AbsoluteUri.GetLeftPart(UriPartial.Authority)}{og.Image.AbsolutePath}"))
                {
                    await downloadStream.CopyToAsync(bufferStream);
                }
                bufferStream.Position = 0;
                return Bitmap.DecodeToWidth(bufferStream, 256);
            }
            else
            {
                return null;
            }
        }
    }
}
