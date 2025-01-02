using Avalonia.Platform.Storage;
using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class TorrentService : ITorrentService, IDisposable
    {
        private readonly Dictionary<string, TorrentManager> managerMap;

        private readonly Dictionary<TorrentManager, Progress<TorrentStatus>> progressMap;

        private readonly IServiceManager serviceManager;

        private readonly ClientEngine engine;

        public TorrentService(IServiceManager serviceManager)
        {
            managerMap = new();
            progressMap = new();

            this.serviceManager = serviceManager;
            engine = new();
        }

        public TorrentService(IServiceManager serviceManager, EngineSettings settings)
        {
            managerMap = new();
            progressMap = new();

            this.serviceManager = serviceManager;
            engine = new(settings);
        }

        public async Task InitializeAsync()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            // Add all outstanding torrents
            await Task.WhenAll(dbService.GetFiles().Select(AddTorrentAsync));

            // Start them
            await Task.WhenAll(dbService.GetFiles().Select(StartAsync));
        }

        public async Task DeinitializeAsync()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            // Stop all outstanding torrents
            await Task.WhenAll(dbService.GetFiles().Select(StopAsync));
        }

        public async Task<bool> AddTorrentAsync(SubverseFile file)
        {
            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "torrent", file.OwnerPeer.ToString()
                    );

            bool keyExists;
            lock (managerMap)
            {
                keyExists = managerMap.ContainsKey(file.MagnetUri);
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

            lock (managerMap)
            {
                managerMap.Add(file.MagnetUri, manager);
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
            lock (managerMap)
            {
                managerMap.Add(magnetUri, manager);
            }

            Progress<TorrentStatus> progress = new ();
            manager.TorrentStateChanged += (s, ev) => 
            ((IProgress<TorrentStatus>)progress).Report(
                new TorrentStatus(
                    manager.Complete, 
                    manager.PartialProgress, 
                    manager.State
                    ));
            lock (progressMap)
            {
                progressMap.Add(manager, progress);
            }

            return new SubverseFile(magnetUri, thisPeer)
            { TorrentBytes = torrent.Encode() };
        }

        public async Task<bool> RemoveTorrentAsync(SubverseFile file)
        {
            TorrentManager? manager;
            lock (managerMap)
            {
                managerMap.Remove(file.MagnetUri, out manager);
            }

            bool wasRemoved;
            lock (progressMap)
            {
                wasRemoved = manager is not null && progressMap.Remove(manager);
            }

            return wasRemoved && await engine.RemoveAsync(
                MagnetLink.Parse(file.MagnetUri),
                RemoveMode.CacheDataAndDownloadedData
                );
        }

        public async Task<Progress<TorrentStatus>?> StartAsync(SubverseFile file)
        {
            bool entryExists;

            TorrentManager? manager;
            Progress<TorrentStatus>? progress = null;

            lock (managerMap)
            {
                entryExists = managerMap.TryGetValue(file.MagnetUri, out manager) &&
                    progressMap.TryGetValue(manager, out progress);
            }

            if (entryExists)
            {
                await manager!.StartAsync();
            }

            return progress;
        }

        public async Task<bool> StopAsync(SubverseFile file)
        {
            bool keyExists;
            TorrentManager? manager;

            lock (managerMap)
            {
                keyExists = managerMap.TryGetValue(file.MagnetUri, out manager);
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
