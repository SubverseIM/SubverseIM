using MonoTorrent;
using MonoTorrent.Client;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TorrentViewModel : ViewModelBase
    {
        private readonly ITorrentService torrentService;

        internal readonly SubverseTorrent innerTorrent;

        private Progress<TorrentStatus>? torrentStatus;

        public string? DisplayName => MagnetLink.Parse(innerTorrent.MagnetUri).Name;

        private double downloadProgress;
        public double DownloadProgress 
        {
            get => downloadProgress;
            private set 
            {
                this.RaiseAndSetIfChanged(ref downloadProgress, value);
            }
        }

        private bool downloadComplete;
        public bool DownloadComplete
        {
            get => downloadComplete;
            private set 
            {
                this.RaiseAndSetIfChanged(ref downloadComplete, value);
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

        public TorrentViewModel(ITorrentService torrentService, SubverseTorrent innerTorrent, Progress<TorrentStatus>? torrentStatus) 
        {
            this.torrentService = torrentService;
            this.innerTorrent = innerTorrent;
            RegisterStatus(torrentStatus);
        }

        private void TorrentProgressChanged(object? sender, TorrentStatus e)
        {
            DownloadComplete = e.Complete;
            DownloadProgress = e.Progress;
            TorrentState = e.State;
        }

        private void RegisterStatus(Progress<TorrentStatus>? torrentStatus) 
        {
            if (this.torrentStatus is not null) 
            {
                this.torrentStatus.ProgressChanged -= TorrentProgressChanged;
            }
            this.torrentStatus = torrentStatus;
            if (torrentStatus is not null) 
            {
                torrentStatus.ProgressChanged += TorrentProgressChanged;
            }
        }

        public Task StartCommandAsync()
        {
            return torrentService.StartAsync(innerTorrent);
        }

        public Task StopCommandAsync() 
        {
            return torrentService.StopAsync(innerTorrent);
        }

        public Task DeleteCommandAsync() 
        {
            return torrentService.RemoveTorrentAsync(innerTorrent);
        }
    }
}
