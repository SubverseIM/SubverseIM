using Avalonia.Media.Imaging;
using MonoTorrent;
using OpenGraphNet;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class EmbedViewModel : ViewModelBase
    {
        private readonly IServiceManager serviceManager;

        public Uri AbsoluteUri { get; }

        public string DisplayName => MagnetLink.TryParse(
            AbsoluteUri.OriginalString, out MagnetLink? magnetLink) ?
            magnetLink.Name ?? "Untitled" :
            AbsoluteUri.Host;

        public Task<Bitmap?> FetchedBitmap { get; }

        public EmbedViewModel(IServiceManager serviceManager, string uriString)
        {
            this.serviceManager = serviceManager;
            AbsoluteUri = new Uri(uriString);
            FetchedBitmap = GetBitmapAsync();
        }

        public async Task<Bitmap?> GetBitmapAsync()
        {
            OpenGraph og;
            ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
            Progress<TorrentStatus>? progress = await torrentService.StartAsync(new SubverseTorrent(AbsoluteUri.OriginalString));
            if (progress is not null)
            {
                TaskCompletionSource tcs = new();
                progress.ProgressChanged += (s, ev) =>
                {
                    if (ev.Complete) tcs.TrySetResult();
                };
                await tcs.Task;

                string cacheDirPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent", "files"
                        );
                string cacheFilePath = Path.Combine(cacheDirPath,
                    DisplayName ?? throw new InvalidOperationException("No display name was provided for this file!")
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
