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

        private readonly Dictionary<InfoHash, TorrentManager> managerMap;

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
            IEnumerable<SubverseTorrent> files = await dbService.GetTorrentsAsync();

            // Add and start all outstanding torrents
            bool[] addedFlags = await Task.WhenAll(files.Select(x => AddTorrentAsync(x.InfoHash, x.TorrentBytes)));
            SubverseTorrent[] addedTorrents = files.Zip(addedFlags)
                .Where(x => x.Second)
                .Select(x => x.First)
                .ToArray();
            await Task.WhenAll(addedTorrents.Select(StartAsync));

            return files.Zip(files
                .Select(x => managerMap[x.InfoHash])
                .Select(x => progressMap[x]))
                .ToFrozenDictionary(x => x.First, x => x.Second!);
        }

        public async Task DestroyAsync()
        {
            // Stop all outstanding torrents
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            await Task.WhenAll((await dbService.GetTorrentsAsync()).Select(StopAsync));
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
                        await dbService.InsertOrUpdateItemAsync(torrent);
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
                        stillExists = managerMap.ContainsKey(torrent.InfoHash);
                    }
                }
            });

            return progress;
        }

        public async Task<bool> AddTorrentAsync(InfoHash infoHash, byte[]? torrentBytes)
        {
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            string destDirPath;
            MagnetLink? magnetLink;
            SubverseTorrent? torrent = await dbService.GetTorrentAsync(infoHash)
                ?? throw new InvalidOperationException("Could not find specified torrent in database.");
            torrent.TorrentBytes = torrentBytes ?? torrent.TorrentBytes;
            await dbService.InsertOrUpdateItemAsync(torrent);

            destDirPath = Path.Combine(
                launcherService.GetPersistentStoragePath(), "torrent", "files", infoHash.ToHex()
                );
            Directory.CreateDirectory(destDirPath);

            bool keyExists;
            lock (managerMap)
            {
                keyExists = managerMap.ContainsKey(torrent.InfoHash);
            }

            TorrentManager manager;
            if (keyExists)
            {
                return false;
            }
            else if (MagnetLink.TryParse(torrent.MagnetUri, out magnetLink))
            {
                try
                {
                    manager = await engine.AddAsync(magnetLink, destDirPath);
                }
                catch (TorrentException)
                {
                    manager = engine.Torrents.Single(x =>
                        x.InfoHashes == magnetLink.InfoHashes
                        );
                }
            }
            else
            {
                throw new ArgumentException("Could not parse magnet link info for specified torrent.");
            }

            lock (managerMap)
            {
                managerMap.TryAdd(torrent.InfoHash, manager);
            }

            lock (progressMap)
            {
                progressMap.TryAdd(manager, CreateProgress(manager, torrent));
            }

            return true;
        }

        public async Task<SubverseTorrent> AddTorrentAsync(IStorageFile file, CancellationToken cancellationToken = default)
        {
            IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();

            string cacheDirPath = Path.Combine(launcherService.GetPersistentStoragePath(), "torrent", "staging");
            Directory.CreateDirectory(cacheDirPath);

            string cacheFilePath = Path.Combine(cacheDirPath, file.Name);
            using (Stream localFileStream = await file.OpenReadAsync())
            using (Stream cacheFileStream = File.Create(cacheFilePath))
            {
                await localFileStream.CopyToAsync(cacheFileStream, cancellationToken);
            }

            TorrentCreator torrentCreator = new(TorrentType.V1V2Hybrid);
            torrentCreator.Announces.Add(await bootstrapperService.GetAnnounceUriListAsync(MAX_ANNOUNCE_COUNT, cancellationToken));

            IReadOnlyList<Uri> webSeedUrls = await frontendService.ShowUploadDialogAsync(cacheFilePath);
            torrentCreator.GetrightHttpSeeds.AddRange(webSeedUrls.Select(x => x.OriginalString));

            BEncodedDictionary metadataDict = await torrentCreator.CreateAsync(new TorrentFileSource(cacheFilePath), cancellationToken);
            Torrent metadata = Torrent.Load(metadataDict);

            string magnetUri = new MagnetLink(
                infoHashes: metadata.InfoHashes,
                name: metadata.Name,
                announceUrls: metadata.AnnounceUrls[0],
                webSeeds: metadata.HttpSeeds.Select(x => x.OriginalString),
                size: metadata.Size
                ).ToV1String();

            SubverseTorrent torrent = new SubverseTorrent(metadata.InfoHashes.V1OrV2, magnetUri)
            {
                TorrentBytes = metadataDict.Encode(),
                DateLastUpdatedOn = DateTime.UtcNow,
            };
            await dbService.InsertOrUpdateItemAsync(torrent);

            TorrentManager manager;
            try
            {
                string destDirPath = Path.Combine(launcherService.GetPersistentStoragePath(), 
                    "torrent", "files", metadata.InfoHashes.V1OrV2.ToHex());
                Directory.CreateDirectory(destDirPath);

                string destFilePath = Path.Combine(destDirPath, file.Name);
                File.Move(cacheFilePath, destFilePath, true);

                manager = await engine.AddAsync(metadata, destDirPath,
                    new TorrentSettingsBuilder { AllowInitialSeeding = true }
                    .ToSettings());
            }
            catch (TorrentException)
            {
                manager = engine.Torrents.Single(x =>
                    x.InfoHashes == metadata.InfoHashes
                    );
            }

            lock (managerMap)
            {
                managerMap.TryAdd(torrent.InfoHash, manager);
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

            SubverseTorrent? storedItem = await dbService.GetTorrentAsync(torrent.InfoHash);
            while (storedItem is not null)
            {
                await dbService.DeleteItemByIdAsync<SubverseTorrent>(storedItem.Id);
                storedItem = await dbService.GetTorrentAsync(torrent.InfoHash);
            }

            TorrentManager? manager;
            lock (managerMap)
            {
                managerMap.Remove(torrent.InfoHash, out manager);
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
                managerMap.TryGetValue(torrent.InfoHash, out manager);
            }

            if (manager is not null)
            {
                lock (progressMap)
                {
                    progressMap.TryGetValue(manager, out progress);
                }

                if (manager.State == TorrentState.Stopped)
                {
                    await manager.StartAsync();
                    await manager.DhtAnnounceAsync();
                }
            }

            return progress;
        }

        public async Task<bool> StopAsync(SubverseTorrent torrent)
        {
            TorrentManager? manager;
            lock (managerMap)
            {
                managerMap.TryGetValue(torrent.InfoHash, out manager);
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
