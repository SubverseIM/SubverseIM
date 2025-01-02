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

        public async Task<IReadOnlyDictionary<SubverseTorrent, Progress<TorrentStatus>>> InitializeAsync()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            // Get all torrents from database
            IEnumerable<SubverseTorrent> files = dbService.GetTorrents();

            // Add and start all outstanding torrents
            return files.Zip(
                    await Task.WhenAll(files.Select(AddTorrentAsync)),
                    await Task.WhenAll(files.Select(StartAsync)))
                .Where(x => x.Second)
                .ToFrozenDictionary(x => x.First, x => x.Third!);
        }

        public async Task DeinitializeAsync()
        {
            // Stop all outstanding torrents
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            await Task.WhenAll(dbService.GetTorrents().Select(StopAsync));
        }

        public async Task<bool> AddTorrentAsync(SubverseTorrent torrent)
        {
            string cacheDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent"
                );

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
            else if (torrent.TorrentBytes is null)
            {
                MagnetLink magnetLink = MagnetLink.Parse(torrent.MagnetUri);
                manager = await engine.AddAsync(magnetLink, cacheDirPath);
            }
            else
            {
                Torrent torrentMetaData = await Torrent.LoadAsync(torrent.TorrentBytes);
                manager = await engine.AddAsync(torrentMetaData, cacheDirPath);
            }

            lock (managerMap)
            {
                managerMap.Add(torrent.MagnetUri, manager);
            }

            return true;
        }

        public async Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            IPeerService peerService = await serviceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
            SubversePeerId thisPeer = await peerService.GetPeerIdAsync();

            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent"
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

            return new SubverseTorrent(magnetUri) { TorrentBytes = torrent.Encode() };
        }

        public async Task<bool> RemoveTorrentAsync(SubverseTorrent torrent)
        {
            TorrentManager? manager;
            lock (managerMap)
            {
                managerMap.Remove(torrent.MagnetUri, out manager);
            }

            bool wasRemoved;
            lock (progressMap)
            {
                wasRemoved = manager is not null && progressMap.Remove(manager);
            }

            return wasRemoved && await engine.RemoveAsync(
                MagnetLink.Parse(torrent.MagnetUri),
                RemoveMode.CacheDataAndDownloadedData
                );
        }

        public async Task<Progress<TorrentStatus>?> StartAsync(SubverseTorrent torrent)
        {
            bool entryExists;

            TorrentManager? manager;
            Progress<TorrentStatus>? progress = null;

            lock (managerMap)
            {
                entryExists = managerMap.TryGetValue(torrent.MagnetUri, out manager) &&
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
