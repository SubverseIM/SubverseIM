using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MonoTorrent;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TorrentViewModel : ViewModelBase
    {
        private const string CONFIRM_TITLE = "Delete File?";
        private const string CONFIRM_MESSAGE = "Warning: this action is semi-permanent and can only be reversed by re-adding the file by clicking a magnet link. Do you want to continue?";

        private readonly TorrentPageViewModel parent;

        internal readonly SubverseTorrent innerTorrent;

        private Progress<TorrentStatus>? torrentProgress;

        public string? DisplayName => innerTorrent.MagnetUri is null ? null :
            MagnetLink.Parse(innerTorrent.MagnetUri).Name;

        private TorrentStatus? torrentStatus;
        public TorrentStatus? CurrentStatus 
        {
            get => torrentStatus;
            set 
            {
                this.RaiseAndSetIfChanged(ref torrentStatus, value);
            }
        }

        private bool isStarted;
        public bool IsStarted
        {
            get => isStarted;
            private set
            {
                this.RaiseAndSetIfChanged(ref isStarted, value);
            }
        }

        public TorrentViewModel(TorrentPageViewModel parent, SubverseTorrent innerTorrent, Progress<TorrentStatus>? torrentProgress)
        {
            this.parent = parent;
            this.innerTorrent = innerTorrent;
            IsStarted = RegisterStatus(torrentProgress);
        }

        private async void TorrentProgressChanged(object? sender, TorrentStatus newStatus)
        {
            if (CurrentStatus is not null && newStatus.Complete != CurrentStatus.Complete)
            {
                INativeService nativeService = await parent.ServiceManager.GetWithAwaitAsync<INativeService>();
                await nativeService.SendPushNotificationAsync(parent.ServiceManager, innerTorrent);
            }

            CurrentStatus = newStatus;
        }

        private bool RegisterStatus(Progress<TorrentStatus>? torrentProgress)
        {
            if (this.torrentProgress is not null)
            {
                this.torrentProgress.ProgressChanged -= TorrentProgressChanged;
            }
            this.torrentProgress = torrentProgress;
            if (torrentProgress is not null)
            {
                torrentProgress.ProgressChanged += TorrentProgressChanged;
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task StartCommand()
        {
            ITorrentService torrentService = await parent.ServiceManager.GetWithAwaitAsync<ITorrentService>();
            IsStarted = RegisterStatus(await torrentService.StartAsync(innerTorrent));
        }

        public async Task StopCommand()
        {
            ITorrentService torrentService = await parent.ServiceManager.GetWithAwaitAsync<ITorrentService>();
            await torrentService.StopAsync(innerTorrent);
            IsStarted = false;
        }

        public async Task DeleteCommand()
        {
            ILauncherService launcherService = await parent.ServiceManager.GetWithAwaitAsync<ILauncherService>();
            ITorrentService torrentService = await parent.ServiceManager.GetWithAwaitAsync<ITorrentService>();
            if (await launcherService.ShowConfirmationDialogAsync(CONFIRM_TITLE, CONFIRM_MESSAGE))
            {
                await torrentService.StopAsync(innerTorrent);

                IsStarted = false;
                parent.Torrents.Remove(this);

                await torrentService.RemoveTorrentAsync(innerTorrent);
            }
        }

        public async Task ShareCommand(Visual? sender)
        {
            string cacheDirPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent", "files"
                        );
            string cacheFilePath = Path.Combine(cacheDirPath,
                DisplayName ?? throw new InvalidOperationException("No display name was provided for this file!")
                );
            try
            {
                ILauncherService launcherService = await parent.ServiceManager.GetWithAwaitAsync<ILauncherService>();
                await launcherService.ShareFileToAppAsync(sender, "Save File As", cacheFilePath);
            }
            catch (PlatformNotSupportedException)
            {
                TopLevel topLevel = await parent.ServiceManager.GetWithAwaitAsync<TopLevel>();
                IStorageFile? saveAsFile = await topLevel.StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title = "Save File As",
                        FileTypeChoices = [FilePickerFileTypes.All],
                        SuggestedFileName = DisplayName,
                    });
                if (saveAsFile is not null)
                {
                    using (Stream cacheFileStream = File.OpenRead(cacheFilePath))
                    using (Stream localFileStream = await saveAsFile.OpenWriteAsync())
                    {
                        await cacheFileStream.CopyToAsync(localFileStream);
                    }
                }
            }
        }
    }
}
