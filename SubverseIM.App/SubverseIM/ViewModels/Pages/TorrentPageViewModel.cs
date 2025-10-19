using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MonoTorrent;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class TorrentPageViewModel : PageViewModelBase<TorrentPageViewModel>
    {
        public override string Title => "File Manager";

        public override bool ShouldConfirmBackNavigation => false;

        public override bool HasSidebar => false;

        public ObservableCollection<TorrentViewModel> Torrents { get; }

        public TorrentPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            Torrents = new();
        }

        public async Task InitializeAsync()
        {
            Torrents.Clear();
            ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>();
            IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>> torrents = await torrentService.InitializeAsync();
            foreach ((SubverseTorrent torrent, Progress<TorrentStatus> status) in torrents)
            {
                Torrents.Add(new(this, torrent, status));
            }
        }

        public async Task ImportCommand()
        {
            ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>();
            TopLevel topLevel = await ServiceManager.GetWithAwaitAsync<TopLevel>();

            IReadOnlyList<IStorageFile> torrentFiles = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Import Torrent",
                    FileTypeFilter = [
                        new FilePickerFileType("BitTorrent")
                        {
                            Patterns = ["*.torrent"],
                            MimeTypes = ["application/x-bittorrent"],
                            AppleUniformTypeIdentifiers = ["com.ChosenFewSoftware.SubverseIM.BitTorrent"],
                        }]
                });
            IStorageFile torrentFile = torrentFiles[0];

            byte[] torrentBytes;
            using (Stream torrentFileStream = await torrentFile.OpenReadAsync())
            using (MemoryStream torrentBytesStream = new())
            {
                await torrentFileStream.CopyToAsync(torrentBytesStream);
                torrentBytes = torrentBytesStream.ToArray();
            }

            Torrent torrent;
            torrent = await Torrent.LoadAsync(torrentBytes);

            string magnetUri = new MagnetLink(torrent.InfoHashes, torrent.Name).ToV1String();
            await torrentService.AddTorrentAsync(magnetUri, torrentBytes);

            await InitializeAsync();
        }

        public async Task DestroyAsync()
        {
            ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>();
            await torrentService.DestroyAsync();
        }
    }
}
