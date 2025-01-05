using Avalonia.Platform.Storage;
using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using SubverseIM.Models;
using System;
using System.Collections.Frozen;
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

        public TorrentService(IServiceManager serviceManager, EngineSettings settings, Factories factories)
        {
            managerMap = new();
            progressMap = new();

            this.serviceManager = serviceManager;
            engine = new(settings, factories);
        }

        public async Task<IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>>> InitializeAsync()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            // Get all torrents from database
            IEnumerable<SubverseTorrent> files = dbService.GetTorrents();

            // Add and start all outstanding torrents
            await Task.WhenAll(files.Select(AddTorrentAsync));

            return files.Zip(await Task.WhenAll(files.Select(StartAsync)))
                .ToFrozenDictionary(x => x.First, x => x.Second!);
        }

        public async Task DestroyAsync()
        {
            // Stop all outstanding torrents
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            await Task.WhenAll(dbService.GetTorrents().Select(StopAsync));
        }

        public async Task<bool> AddTorrentAsync(SubverseTorrent torrent)
        {
            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent", "files"
                    );
            Directory.CreateDirectory(cacheDirPath);

            bool keyExists;
            lock (managerMap)
            {
                keyExists = managerMap.ContainsKey(torrent.MagnetUri);
            }

            TorrentManager manager;
            if (keyExists)
            {
                return false;
            }
            else if (torrent.TorrentBytes is not null)
            {
                Torrent torrentMetaData = await Torrent.LoadAsync(torrent.TorrentBytes);
                manager = await engine.AddAsync(torrentMetaData, cacheDirPath);
            }
            else
            {
                MagnetLink magnetLink = MagnetLink.Parse(torrent.MagnetUri);
                manager = await engine.AddAsync(magnetLink, cacheDirPath);
            }

            lock (managerMap)
            {
                managerMap.Add(torrent.MagnetUri, manager);
            }

            Progress<TorrentStatus> progress = new();
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

            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            dbService.InsertOrUpdateItem(torrent);

            return true;
        }

        public async Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            IPeerService peerService = await serviceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
            SubversePeerId thisPeer = await peerService.GetPeerIdAsync();

            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent", "files"
                    );
            Directory.CreateDirectory(cacheDirPath);

            string cacheFilePath = Path.Combine(cacheDirPath, file.Name);
            using (Stream localFileStream = await file.OpenReadAsync())
            using (Stream cacheFileStream = File.Create(cacheFilePath))
            {
                await localFileStream.CopyToAsync(cacheFileStream, cancellationToken);
            }

            TorrentCreator torrentCreator = new(TorrentType.V1V2Hybrid);
            BEncodedDictionary metadata = await torrentCreator.CreateAsync(new TorrentFileSource(cacheFilePath), cancellationToken);

            TorrentManager manager = await engine.AddAsync(
                Torrent.Load(metadata), cacheDirPath,
                new TorrentSettingsBuilder { AllowInitialSeeding = true }
                .ToSettings());
            string magnetUri = manager.MagnetLink.ToV1String();
            lock (managerMap)
            {
                managerMap.Add(magnetUri, manager);
            }

            Progress<TorrentStatus> progress = new();
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

            SubverseTorrent torrent = new SubverseTorrent(magnetUri) { TorrentBytes = metadata.Encode() };
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            dbService.InsertOrUpdateItem(torrent);

            return torrent;
        }

        public async Task<bool> RemoveTorrentAsync(SubverseTorrent torrent)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            SubverseTorrent? storedItem = dbService.GetTorrent(torrent.MagnetUri);
            if (storedItem is not null)
            {
                dbService.DeleteItemById<SubverseTorrent>(storedItem.Id);
            }

            TorrentManager? manager;
            lock (managerMap) 
            {
                managerMap.Remove(torrent.MagnetUri, out manager);
            }

            if (manager is not null)
            {
                lock (progressMap)
                {
                    progressMap.Remove(manager);
                }

                return await engine.RemoveAsync(manager, RemoveMode.CacheDataAndDownloadedData);
            }
            else 
            {
                return false;
            }
        }

        public async Task<Progress<TorrentStatus>?> StartAsync(SubverseTorrent torrent)
        {
            bool entryExists;

            TorrentManager? manager = null;
            Progress<TorrentStatus>? progress = null;

            lock (managerMap)
            {
                entryExists = torrent.MagnetUri is not null &&
                    managerMap.TryGetValue(torrent.MagnetUri, out manager) &&
                    progressMap.TryGetValue(manager, out progress);
            }

            if (entryExists)
            {
                await manager!.StartAsync();
            }

            return progress;
        }

        public async Task<bool> StopAsync(SubverseTorrent torrent)
        {
            bool keyExists;
            TorrentManager? manager;

            lock (managerMap)
            {
                keyExists = managerMap.TryGetValue(torrent.MagnetUri, out manager);
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
