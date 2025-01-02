using MonoTorrent;
using MonoTorrent.Client;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TorrentViewModel : ViewModelBase
    {
        private const string CONFIRM_TITLE = "Delete File?";
        private const string CONFIRM_MESSAGE = "Warning: this action is semi-permanent and can only be reversed by re-adding the file by clicking a magnet link. Do you want to continue?";

        private readonly TorrentPageViewModel parent;

        internal readonly SubverseTorrent innerTorrent;

        private Progress<TorrentStatus>? torrentStatus;

        public string? DisplayName => MagnetLink.Parse(innerTorrent.MagnetUri).Name;

        private bool downloadComplete;
        public bool DownloadComplete
        {
            get => downloadComplete;
            private set
            {
                this.RaiseAndSetIfChanged(ref downloadComplete, value);
            }
        }

        private double downloadProgress;
        public double DownloadProgress 
        {
            get => downloadProgress;
            private set 
            {
                this.RaiseAndSetIfChanged(ref downloadProgress, value);
            }
        }

        private TorrentState torrentState;
        public TorrentState TorrentState
        {
            get => torrentState;
            private set 
            {
                this.RaiseAndSetIfChanged(ref torrentState, value);
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

        public TorrentViewModel(TorrentPageViewModel parent, SubverseTorrent innerTorrent, Progress<TorrentStatus>? torrentStatus) 
        {
            this.parent = parent;
            this.innerTorrent = innerTorrent;
            IsStarted = RegisterStatus(torrentStatus);
        }

        private void TorrentProgressChanged(object? sender, TorrentStatus e)
        {
            DownloadComplete = e.Complete;
            DownloadProgress = e.Progress;
            TorrentState = e.State;
        }

        private bool RegisterStatus(Progress<TorrentStatus>? torrentStatus) 
        {
            if (this.torrentStatus is not null) 
            {
                this.torrentStatus.ProgressChanged -= TorrentProgressChanged;
            }
            this.torrentStatus = torrentStatus;
            if (torrentStatus is not null)
            {
                torrentStatus.ProgressChanged += TorrentProgressChanged;
                return true;
            }
            else 
            {
                return false;
            }
        }

        public async Task StartCommandAsync()
        {
            ITorrentService torrentService = await parent.ServiceManager.GetWithAwaitAsync<ITorrentService>();
            IsStarted = RegisterStatus(await torrentService.StartAsync(innerTorrent));
        }

        public async Task StopCommandAsync() 
        {
            ITorrentService torrentService = await parent.ServiceManager.GetWithAwaitAsync<ITorrentService>();
            await torrentService.StopAsync(innerTorrent);
            torrentStatus = null;
            IsStarted = false;
        }

        public async Task DeleteCommandAsync() 
        {
            ILauncherService launcherService = await parent.ServiceManager.GetWithAwaitAsync<ILauncherService>();
            ITorrentService torrentService = await parent.ServiceManager.GetWithAwaitAsync<ITorrentService>();
            IDbService dbService = await parent.ServiceManager.GetWithAwaitAsync<IDbService>();

            if (await launcherService.ShowConfirmationDialogAsync(CONFIRM_TITLE, CONFIRM_MESSAGE))
            {
                await torrentService.StopAsync(innerTorrent);

                torrentStatus = null;
                IsStarted = false;

                await torrentService.RemoveTorrentAsync(innerTorrent);
                dbService.DeleteItemById<SubverseTorrent>(innerTorrent.Id);
            }
        }
    }
}
