using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    public class TorrentService : ITorrentService, IDisposableService
    {
        private const int MAX_ANNOUNCE_COUNT = 3;

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
            await Task.WhenAll(files.Select(x => AddTorrentAsync(x.MagnetUri, x.TorrentBytes)));

            return files.Zip(await Task.WhenAll(files.Select(StartAsync)))
                .ToFrozenDictionary(x => x.First, x => x.Second!);
        }

        public async Task DestroyAsync()
        {
            // Stop all outstanding torrents
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            await Task.WhenAll(dbService.GetTorrents().Select(StopAsync));
        }

        private Progress<TorrentStatus> CreateProgress(TorrentManager manager, SubverseTorrent torrent)
        {
            Progress<TorrentStatus> progress = new();

            Dispatcher.UIThread.InvokeAsync(async Task () =>
            {
                using PeriodicTimer timer = new(TimeSpan.FromSeconds(1.5));
                IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

                bool stillExists = true;
                while (stillExists)
                {
                    if (manager.HasMetadata)
                    {
                        torrent.TorrentBytes = File.ReadAllBytes(manager.MetadataPath);
                        dbService.InsertOrUpdateItem(torrent);
                    }

                    ((IProgress<TorrentStatus>)progress)
                    .Report(new TorrentStatus(
                        manager.Complete || manager.PartialProgress == 100.0,
                        manager.HasMetadata,
                        manager.PartialProgress
                        ));

                    await timer.WaitForNextTickAsync();

                    lock (managerMap)
                    {
                        stillExists = managerMap.ContainsKey(torrent.MagnetUri);
                    }
                }
            });

            return progress;
        }

        public async Task<bool> AddTorrentAsync(string magnetUri, byte[]? torrentBytes)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            SubverseTorrent? torrent = dbService.GetTorrent(magnetUri) ??
                new SubverseTorrent(magnetUri)
                {
                    DateLastUpdatedOn = DateTime.UtcNow
                };
            torrent.TorrentBytes = torrentBytes ?? torrent.TorrentBytes;
            dbService.InsertOrUpdateItem(torrent);

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
                try
                {
                    manager = await engine.AddAsync(torrentMetaData, cacheDirPath);
                }
                catch (TorrentException)
                {
                    manager = engine.Torrents.Single(x =>
                        x.InfoHashes == torrentMetaData.InfoHashes
                        );
                }
            }
            else
            {
                MagnetLink magnetLink = MagnetLink.Parse(torrent.MagnetUri);
                try
                {
                    manager = await engine.AddAsync(magnetLink, cacheDirPath);
                }
                catch (TorrentException)
                {
                    manager = engine.Torrents.Single(x =>
                        x.InfoHashes == magnetLink.InfoHashes
                        );
                }
            }

            lock (managerMap)
            {
                managerMap.Add(torrent.MagnetUri, manager);
            }

            lock (progressMap)
            {
                progressMap.Add(manager, CreateProgress(manager, torrent));
            }

            return true;
        }

        public async Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            string cacheDirPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent", "files"
                    );
            string cacheFilePath = Path.Combine(cacheDirPath, file.Name);
            if (!File.Exists(cacheFilePath))
            {
                Directory.CreateDirectory(cacheDirPath);

                using (Stream localFileStream = await file.OpenReadAsync())
                using (Stream cacheFileStream = File.Create(cacheFilePath))
                {
                    await localFileStream.CopyToAsync(cacheFileStream, cancellationToken);
                }
            }

            TorrentCreator torrentCreator = new(TorrentType.V1V2Hybrid);
            torrentCreator.Announces.Add(await bootstrapperService.GetAnnounceUriListAsync(MAX_ANNOUNCE_COUNT, cancellationToken));

            BEncodedDictionary metadataDict = await torrentCreator.CreateAsync(new TorrentFileSource(cacheFilePath), cancellationToken);
            Torrent metadata = Torrent.Load(metadataDict);

            TorrentManager manager;
            try
            {
                manager = await engine.AddAsync(metadata, cacheDirPath,
                    new TorrentSettingsBuilder { AllowInitialSeeding = true }
                    .ToSettings());
            }
            catch (TorrentException)
            {
                manager = engine.Torrents.Single(x =>
                    x.InfoHashes == metadata.InfoHashes
                    );
            }

            string magnetUri = new MagnetLink(
                infoHashes: metadata.InfoHashes,
                name: metadata.Name,
                announceUrls: metadata.AnnounceUrls[0],
                size: metadata.Size
                ).ToV1String();

            SubverseTorrent torrent = new SubverseTorrent(magnetUri)
            {
                TorrentBytes = metadataDict.Encode(),
                DateLastUpdatedOn = DateTime.UtcNow,
            };
            dbService.InsertOrUpdateItem(torrent);

            lock (managerMap)
            {
                managerMap.TryAdd(magnetUri, manager);
            }

            lock (progressMap)
            {
                progressMap.TryAdd(manager, CreateProgress(manager, torrent));
            }

            return torrent;
        }

        public async Task<bool> RemoveTorrentAsync(SubverseTorrent torrent)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubverseTorrent? storedItem = dbService.GetTorrent(torrent.MagnetUri);
            while (storedItem is not null)
            {
                dbService.DeleteItemById<SubverseTorrent>(storedItem.Id);
                storedItem = dbService.GetTorrent(torrent.MagnetUri);
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
            TorrentManager? manager = null;
            Progress<TorrentStatus>? progress = null;

            lock (managerMap)
            {
                managerMap.TryGetValue(torrent.MagnetUri, out manager);
            }

            if (manager is not null)
            {
                lock (progressMap)
                {
                    progressMap.TryGetValue(manager, out progress);
                }

                await manager.StartAsync();
                await manager.DhtAnnounceAsync();
            }

            return progress;
        }

        public async Task<bool> StopAsync(SubverseTorrent torrent)
        {
            TorrentManager? manager;
            lock (managerMap)
            {
                managerMap.TryGetValue(torrent.MagnetUri, out manager);
            }

            HashSet<TorrentState> invalidStates = [TorrentState.Stopping, TorrentState.Stopped];
            if (manager is not null && !invalidStates.Contains(manager.State))
            {
                await manager.StopAsync();
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
