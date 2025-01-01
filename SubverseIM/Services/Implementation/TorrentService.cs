using Avalonia.Platform.Storage;
using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class TorrentService : ITorrentService, IDisposable
    {
        private readonly Dictionary<string, TorrentManager> managers;

        private readonly IServiceManager serviceManager;

        private readonly ClientEngine engine;

        public TorrentService(IServiceManager serviceManager)
        {
            managers = new Dictionary<string, TorrentManager>();

            this.serviceManager = serviceManager;
            engine = new();
        }

        public TorrentService(IServiceManager serviceManager, EngineSettings settings)
        {
            managers = new Dictionary<string, TorrentManager>();

            this.serviceManager = serviceManager;
            engine = new(settings);
        }

        public async Task<bool> AddTorrentAsync(SubverseFile file)
        {
            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "torrent", file.OwnerPeer.ToString()
                    );

            bool keyExists;
            lock (managers)
            {
                keyExists = managers.ContainsKey(file.MagnetUri);
            }

            TorrentManager manager;
            if (keyExists)
            {
                return false;
            }
            else if (file.TorrentBytes is null)
            {
                MagnetLink magnetLink = MagnetLink.Parse(file.MagnetUri);
                manager = await engine.AddAsync(magnetLink, cacheDirPath);
            }
            else 
            {
                Torrent torrent = await Torrent.LoadAsync(file.TorrentBytes);
                manager = await engine.AddAsync(torrent, cacheDirPath);
            }

            lock (managers)
            {
                managers.Add(file.MagnetUri, manager);
            }

            return true;
        }

        public async Task<SubverseFile> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            IPeerService peerService = await serviceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
            SubversePeerId thisPeer = await peerService.GetPeerIdAsync();

            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "torrent", thisPeer.ToString()
                    );
            string cacheFilePath = Path.Combine(cacheDirPath, file.Name);

            using (Stream localFileStream = await file.OpenReadAsync())
            using (Stream cacheFileStream = File.Create(cacheFilePath))
            {
                await localFileStream.CopyToAsync(cacheFileStream, cancellationToken);
            }

            TorrentCreator torrentCreator = new(TorrentType.V1V2Hybrid);
            BEncodedDictionary torrent = await torrentCreator.CreateAsync(new TorrentFileSource(cacheFilePath), cancellationToken);

            TorrentManager manager = await engine.AddAsync(Torrent.Load(torrent), cacheDirPath);
            string magnetUri = manager.MagnetLink.ToString()!;

            lock (manager)
            {
                managers.Add(magnetUri, manager);
            }

            return new SubverseFile(magnetUri, thisPeer) 
            { TorrentBytes = torrent.Encode() };
        }

        public async Task<bool> RemoveTorrentAsync(SubverseFile file)
        {
            bool wasRemoved;
            lock (managers)
            {
                wasRemoved = managers.Remove(file.MagnetUri);
            }

            return wasRemoved && await engine.RemoveAsync(
                MagnetLink.Parse(file.MagnetUri),
                RemoveMode.CacheDataAndDownloadedData
                );
        }

        public async Task<Progress<double>?> StartAsync(SubverseFile file)
        {
            bool keyExists;
            TorrentManager? manager;

            lock (managers) 
            {
                keyExists = managers.TryGetValue(file.MagnetUri, out manager);
            }

            if (keyExists)
            {
                Progress<double> progress = new Progress<double>();
                manager!.TorrentStateChanged += (s, ev) => ((IProgress<double>)progress).Report(manager.Progress);

                await manager.StartAsync();
                return progress;
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> StopAsync(SubverseFile file)
        {
            bool keyExists;
            TorrentManager? manager;

            lock (managers)
            {
                keyExists = managers.TryGetValue(file.MagnetUri, out manager);
            }

            if (keyExists)
            {
                await manager!.StopAsync();
                return true;
            }
            else 
            {
                return false;
            }
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    engine.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
