using Avalonia;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;
using MonoTorrent.PortForwarding;
using PgpCore;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SubverseIM.Services.Implementation
{
    public class BootstrapperService : IBootstrapperService, IDisposableService, IInjectable
    {
        private record struct CachedTopicId(DateTime Timestamp, SubversePeerId TopicId);

        private const int TOPIC_CACHE_EXPIRE_MINUTES = 3;

        private const string TRACKERS_LIST_URI = "https://raw.githubusercontent.com/ngosang/trackerslist/master/trackers_best.txt";

        private static readonly IReadOnlyDictionary<string, TimeSpan> expireTimes =
            new Dictionary<string, TimeSpan>()
            {
                { "90 minutes", TimeSpan.FromMinutes(90) },
                { "24 hours", TimeSpan.FromHours(24) },
                { "3 days", TimeSpan.FromDays(3) }
            };

        private readonly INativeService nativeService;

        private readonly HttpClient httpClient;

        private readonly ConcurrentDictionary<string, CachedTopicId> cachedTopicIds;

        private readonly ConcurrentBag<TaskCompletionSource<IList<PeerInfo>>> peerInfoBag;

        private readonly TaskCompletionSource<IDhtEngine> dhtEngineTcs;

        private readonly TaskCompletionSource<IDhtListener> dhtListenerTcs;

        private readonly TaskCompletionSource<IPortForwarder> portForwarderTcs;

        private readonly TaskCompletionSource<IServiceManager> serviceManagerTcs;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        private readonly PeriodicTimer periodicTimer;

        public BootstrapperService(INativeService nativeService)
        {
            this.nativeService = nativeService;

            httpClient = new();

            cachedTopicIds = new();
            peerInfoBag = new();

            dhtEngineTcs = new();
            dhtListenerTcs = new();
            portForwarderTcs = new();
            serviceManagerTcs = new();

            thisPeerTcs = new();
            periodicTimer = new(TimeSpan.FromSeconds(5));
        }

        #region IBootstrapperService API

        private void DhtPeersFound(object? sender, PeersFoundEventArgs e)
        {
            TaskCompletionSource<IList<PeerInfo>>? tcs;
            if (!peerInfoBag.TryTake(out tcs))
            {
                peerInfoBag.Add(tcs = new());
            }
            tcs.TrySetResult(e.Peers);
        }

        private async Task<(Stream, Stream)> GenerateKeysIfNoneAsync()
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            Stream? publicKeyStream = await dbService.GetReadStreamAsync(IDbService.PUBLIC_KEY_PATH);
            Stream? privateKeyStream = await dbService.GetReadStreamAsync(IDbService.PRIVATE_KEY_PATH);
            if (publicKeyStream is not null && privateKeyStream is not null)
            {
                return (publicKeyStream, privateKeyStream);
            }
            else
            {
                publicKeyStream = new MemoryStream();
                privateKeyStream = new MemoryStream();

                using (PGP pgp = new())
                {
                    pgp.GenerateKey(
                        publicKeyStream,
                        privateKeyStream,
                        password: IDbService.SECRET_PASSWORD
                        );
                }

                using (Stream publicKeyStoreStream = await dbService.CreateWriteStreamAsync(IDbService.PUBLIC_KEY_PATH))
                using (Stream privateKeyStoreStream = await dbService.CreateWriteStreamAsync(IDbService.PRIVATE_KEY_PATH))
                {
                    publicKeyStream.Position = 0;
                    publicKeyStream.CopyTo(publicKeyStoreStream);

                    privateKeyStream.Position = 0;
                    privateKeyStream.CopyTo(privateKeyStoreStream);
                }

                publicKeyStream.Position = 0;
                privateKeyStream.Position = 0;

                return (publicKeyStream, privateKeyStream);
            }
        }

        private async Task<bool> SynchronizePeersAsync(Uri bootstrapperUri, CancellationToken cancellationToken)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task;
            IServiceManager serviceManager = await serviceManagerTcs.Task;

            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            IMessageService messageService = await serviceManager.GetWithAwaitAsync<IMessageService>();
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            try
            {
                SubversePeer peer;
                SubversePeerId thisPeer = await GetPeerIdAsync(cancellationToken);
                lock (messageService.CachedPeers)
                {
                    peer = messageService.CachedPeers[thisPeer];
                }

                ReadOnlyMemory<byte> nodesBytes = await dhtEngine.SaveNodesAsync();
                using (Stream cacheStream = await dbService.CreateWriteStreamAsync(IDbService.NODES_LIST_PATH))
                {
                    cacheStream.Write(nodesBytes.Span);
                }

                byte[] requestBytes;
                using (PGP pgp = new(peer.KeyContainer))
                using (MemoryStream inputStream = new(nodesBytes.ToArray()))
                using (MemoryStream outputStream = new())
                {
                    await pgp.SignAsync(inputStream, outputStream);
                    requestBytes = outputStream.ToArray();
                }

                Uri requestUri = new Uri(bootstrapperUri, $"nodes?p={thisPeer}");
                using (ByteArrayContent requestContent = new(requestBytes)
                { Headers = { ContentType = new("application/octet-stream") } })
                {
                    HttpResponseMessage response = await httpClient.PostAsync(requestUri, requestContent, cancellationToken);
                    return await response.Content.ReadFromJsonAsync<bool>(cancellationToken);
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SynchronizePeersAsync(Uri bootstrapperUri, SubversePeerId peerId, CancellationToken cancellationToken)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task.WaitAsync(cancellationToken);
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(new Uri(bootstrapperUri, $"nodes?p={peerId}"), cancellationToken);
                byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                dhtEngine.Add([responseBytes]);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SubmitDeviceTokenAsync(Uri bootstrapperUri, CancellationToken cancellationToken) 
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task;

            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            IMessageService messageService = await serviceManager.GetWithAwaitAsync<IMessageService>();

            byte[]? deviceToken = launcherService.GetDeviceToken();
            if (deviceToken is null) return false;

            try
            {
                SubversePeer peer;
                SubversePeerId thisPeer = await GetPeerIdAsync(cancellationToken);
                lock (messageService.CachedPeers)
                {
                    peer = messageService.CachedPeers[thisPeer];
                }

                byte[] requestBytes;
                using (PGP pgp = new(peer.KeyContainer))
                using (MemoryStream inputStream = new(deviceToken))
                using (MemoryStream outputStream = new())
                {
                    await pgp.SignAsync(inputStream, outputStream);
                    requestBytes = outputStream.ToArray();
                }

                Uri requestUri = new Uri(bootstrapperUri, $"token?p={thisPeer}");
                using (ByteArrayContent requestContent = new(requestBytes)
                { Headers = { ContentType = new("application/octet-stream") } })
                {
                    HttpResponseMessage response = await httpClient.PostAsync(requestUri, requestContent, cancellationToken);
                    return await response.Content.ReadFromJsonAsync<bool>(cancellationToken);
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task BootstrapSelfAsync(CancellationToken cancellationToken)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task.WaitAsync(cancellationToken);

            IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            IMessageService messageService = await serviceManager.GetWithAwaitAsync<IMessageService>(cancellationToken);

            SubverseConfig config = await configurationService.GetConfigAsync(cancellationToken);

            IDhtEngine dhtEngine = await dhtEngineTcs.Task.WaitAsync(cancellationToken);
            IDhtListener dhtListener = await dhtListenerTcs.Task.WaitAsync(cancellationToken);
            IPortForwarder portForwarder = await portForwarderTcs.Task.WaitAsync(cancellationToken);

            // (Re)initialize DHT client
            dhtEngine.PeersFound += DhtPeersFound;
            await dhtEngine.SetListenerAsync(dhtListener);
            using (MemoryStream bufferStream = new())
            {
                Stream? cacheStream = await dbService.GetReadStreamAsync(IDbService.NODES_LIST_PATH);
                if (cacheStream is not null)
                {
                    await cacheStream.CopyToAsync(bufferStream);
                    await dhtEngine.StartAsync(bufferStream.ToArray());
                }
                else
                {
                    await dhtEngine.StartAsync();
                }

                cacheStream?.Dispose();
            }

            // Announce public key on bootstrapper
            Stream? pkStream = await dbService.GetReadStreamAsync(IDbService.PUBLIC_KEY_PATH);
            if (pkStream is not null)
            {
                foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
                {
                    using (pkStream)
                    using (StreamContent pkStreamContent = new(pkStream)
                    { Headers = { ContentType = new("application/pgp-keys") } })
                    {
                        await httpClient.PostAsync(new Uri(bootstrapperUri, "pk"), pkStreamContent, cancellationToken);
                    }
                }
            }

            // Submit device token to bootstrapper
            if (config.IsPushNotificationsEnabled)
            {
                foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
                {
                    await SubmitDeviceTokenAsync(bootstrapperUri, cancellationToken);
                }
            }

            // Forward SIP ports
            await portForwarder.StartAsync(cancellationToken);

            int portNum, retryCount = 0;
            Mapping? mapping = portForwarder.Mappings.Created.SingleOrDefault(x =>
                    x.PrivatePort == IBootstrapperService.DEFAULT_PORT_NUMBER
                    );
            for (portNum = IBootstrapperService.DEFAULT_PORT_NUMBER; retryCount++ < 3 && mapping is null; portNum++)
            {
                if (!portForwarder.Active) break;

                await portForwarder.RegisterMappingAsync(new Mapping(Protocol.Udp, IBootstrapperService.DEFAULT_PORT_NUMBER, portNum));
                await periodicTimer.WaitForNextTickAsync(cancellationToken);

                mapping = portForwarder.Mappings.Created.SingleOrDefault(x =>
                    x.PrivatePort == IBootstrapperService.DEFAULT_PORT_NUMBER
                    );
            }

            // Perform synchronization activities
            while (!cancellationToken.IsCancellationRequested)
            {
                SubversePeerId[] peers;
                lock (messageService.CachedPeers)
                {
                    peers = messageService.CachedPeers.Keys.ToArray();
                }

                foreach (SubversePeerId topicId in await GetTopicIdsAsync(cancellationToken))
                {
                    await periodicTimer.WaitForNextTickAsync(cancellationToken);
                    dhtEngine.Announce(new InfoHash(topicId.GetBytes()),
                        mapping?.PublicPort ?? portNum);
                }

                foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
                {
                    if (await SynchronizePeersAsync(bootstrapperUri, cancellationToken))
                    {
                        await periodicTimer.WaitForNextTickAsync(cancellationToken);
                    }

                    foreach (SubversePeerId otherPeer in peers)
                    {
                        if (await SynchronizePeersAsync(bootstrapperUri, otherPeer, cancellationToken))
                        {
                            await periodicTimer.WaitForNextTickAsync(cancellationToken);
                        }
                    }
                }
            }

            // Shutdown all bootstrapping traffic
            await dhtEngine.StopAsync();
            await portForwarder.StopAsync(true, default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task<List<string>> GetAnnounceUriListAsync(int maxCount, CancellationToken cancellationToken)
        {
            List<string> announceUriList = new();

            using Stream httpStream = await httpClient.GetStreamAsync(TRACKERS_LIST_URI, cancellationToken);
            using StreamReader reader = new(httpStream);

            int i = 0;
            string? line;
            do
            {
                line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) continue;

                announceUriList.Add(line);
            } while (line is not null && ++i < maxCount);

            return announceUriList;
        }

        public Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return thisPeerTcs.Task.WaitAsync(cancellationToken);
        }

        public async Task<IList<PeerInfo>> GetPeerInfoAsync(SubversePeerId topicId, CancellationToken cancellationToken)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task.WaitAsync(cancellationToken);
            dhtEngine.GetPeers(new InfoHash(topicId.GetBytes()));

            if (!peerInfoBag.TryPeek(out TaskCompletionSource<IList<PeerInfo>>? tcs))
            {
                peerInfoBag.Add(tcs = new());
            }
            return await tcs.Task.WaitAsync(cancellationToken);
        }

        public async Task<EncryptionKeys> GetPeerKeysAsync(SubversePeerId otherPeer, CancellationToken cancellationToken)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task.WaitAsync(cancellationToken);

            IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            IMessageService messageService = await serviceManager.GetWithAwaitAsync<IMessageService>(cancellationToken);

            SubverseConfig config = await configurationService.GetConfigAsync(cancellationToken);

            SubversePeer? peer;
            lock (messageService.CachedPeers)
            {
                messageService.CachedPeers.TryGetValue(otherPeer, out peer);
            }

            EncryptionKeys? peerKeys;
            if (peer is not null && peer.KeyContainer is null)
            {
                MemoryStream publicKeyStream = new MemoryStream();
                foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
                {
                    try
                    {
                        using (Stream responseStream = await httpClient.GetStreamAsync(new Uri(bootstrapperUri, $"pk?p={otherPeer}"), cancellationToken))
                        {
                            await responseStream.CopyToAsync(publicKeyStream, cancellationToken);
                            publicKeyStream.Position = 0;
                        }
                        break;
                    }
                    catch (HttpRequestException) { }
                }

                Stream? privateKeyStream = await dbService.GetReadStreamAsync(IDbService.PRIVATE_KEY_PATH);
                if (privateKeyStream is not null)
                {
                    peerKeys = new(publicKeyStream, privateKeyStream, IDbService.SECRET_PASSWORD);
                    peer.KeyContainer = peerKeys;

                    publicKeyStream.Dispose();
                    privateKeyStream.Dispose();
                }
                else
                {
                    throw new InvalidOperationException("Could not find private key file in application database!");
                }
            }
            else if (peer?.KeyContainer is not null && peer.KeyContainer.PublicKey is not null)
            {
                peerKeys = peer.KeyContainer;
            }
            else
            {
                throw new InvalidOperationException($"Could not find public key for Peer ID: {otherPeer}");
            }

            return peerKeys;
        }

        public async Task<IReadOnlyList<SubversePeerId>> GetTopicIdsAsync(CancellationToken cancellationToken = default)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task.WaitAsync(cancellationToken);
            IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
            SubverseConfig config = await configurationService.GetConfigAsync(cancellationToken);

            List<SubversePeerId> topicIds = new();
            foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
            {
                if (cachedTopicIds.TryGetValue(bootstrapperUri.OriginalString, out CachedTopicId cachedTopicId) &&
                    (DateTime.UtcNow - cachedTopicId.Timestamp).TotalMinutes < TOPIC_CACHE_EXPIRE_MINUTES)
                {
                    topicIds.Add(cachedTopicId.TopicId);
                }
                else
                {
                    string? topicIdStr = await httpClient.GetFromJsonAsync<string>(new Uri(bootstrapperUri, "topic"));
                    if (!string.IsNullOrEmpty(topicIdStr))
                    {
                        SubversePeerId newTopicId = SubversePeerId.FromString(topicIdStr);
                        cachedTopicIds.AddOrUpdate(bootstrapperUri.OriginalString,
                            (x, t) => new(t, newTopicId), (x, oldValue, t) => new(t, newTopicId),
                            DateTime.UtcNow);
                        topicIds.Add(newTopicId);
                    }
                }
            }

            return topicIds;
        }

        public async Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task.WaitAsync(cancellationToken);
            IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);

            string[] pickerItems = expireTimes.Keys.ToArray();
            string? expireTimeKey = await launcherService.ShowPickerDialogAsync("Link should expire in...", pickerItems[0], pickerItems);

            if (expireTimeKey is null || !expireTimes.TryGetValue(expireTimeKey, out TimeSpan expireTimeValue)) return;
            SubversePeerId thisPeer = await GetPeerIdAsync(cancellationToken);

            SubverseConfig config = await configurationService.GetConfigAsync(cancellationToken);
            config.DefaultContactName = await launcherService.ShowInputDialogAsync("Suggest contact name for recipient?", config.DefaultContactName);
            await configurationService.PersistConfigAsync(cancellationToken);

            string bootstrapperUri = config.BootstrapperUriList?.FirstOrDefault() ??
                IBootstrapperService.DEFAULT_BOOTSTRAPPER_ROOT;
            StringBuilder requestUriBuilder = new(bootstrapperUri);

            requestUriBuilder.Append("/invite?p=");
            requestUriBuilder.Append(thisPeer);

            requestUriBuilder.Append("&t=");
            requestUriBuilder.Append(HttpUtility.UrlEncode(expireTimeValue
                .TotalHours.ToString(CultureInfo.InvariantCulture)));

            if (!string.IsNullOrEmpty(config.DefaultContactName))
            {
                requestUriBuilder.Append("&n=");
                requestUriBuilder.Append(
                    HttpUtility.UrlEncode(config.DefaultContactName)
                    );
            }

            string inviteId = await httpClient.GetFromJsonAsync<string>(requestUriBuilder.ToString(), cancellationToken) ??
                throw new InvalidOperationException("Failed to resolve inviteId!");
            await launcherService.ShareUrlToAppAsync(sender, "Send Invite Via App", $"{bootstrapperUri}/invite/{inviteId}");
        }

        #endregion

        #region IInjectable API

        public async Task InjectAsync(IServiceManager serviceManager)
        {
            // DI container init

            serviceManagerTcs.SetResult(serviceManager);
            serviceManager.GetOrRegister(nativeService);

            // DB service init

            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            await dbService.GetMessagesWithPeersOnTopicAsync([], null);

            (Stream publicKeyStream, Stream privateKeyStream) =
                await GenerateKeysIfNoneAsync();
            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, IDbService.SECRET_PASSWORD);

            publicKeyStream.Dispose();
            privateKeyStream.Dispose();

            // Configuration service init

            serviceManager.GetOrRegister<IConfigurationService>(
                new ConfigurationService(serviceManager)
                );

            // Message service init

            SubversePeerId thisPeer = new(myKeys.PublicKey.GetFingerprint());
            thisPeerTcs.SetResult(thisPeer);

            IMessageService messageService = serviceManager.GetOrRegister
                <IMessageService>(new MessageService(serviceManager));
            lock (messageService.CachedPeers)
            {
                messageService.CachedPeers.Add(thisPeer, new SubversePeer
                {
                    OtherPeer = thisPeer,
                    KeyContainer = myKeys
                });
            }

            // Relay service init

            IRelayService relayService = serviceManager.GetOrRegister
                <IRelayService>(new RelayService());

            // Blob service init

            IBlobService blobService = new BlobService(this, httpClient);
            serviceManager.GetOrRegister(blobService);

            // Torrent service init

            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            Factories factories = Factories.Default
                .WithDhtCreator(() =>
                {
                    var engine = new DhtEngine();
                    dhtEngineTcs.SetResult(engine);
                    return engine;
                })
                .WithDhtListenerCreator(endPoint =>
                {
                    var listener = new DhtListener(endPoint);
                    dhtListenerTcs.SetResult(listener);
                    return listener;
                })
                .WithPortForwarderCreator(() =>
                {
                    var portForwarder = new MonoNatPortForwarder();
                    portForwarderTcs.SetResult(portForwarder);
                    return portForwarder;
                });
            string cacheDirPath = Path.Combine(launcherService.GetPersistentStoragePath(), "torrent");
            serviceManager.GetOrRegister<ITorrentService>(
                new TorrentService(serviceManager, new EngineSettingsBuilder
                { 
                    CacheDirectory = cacheDirPath,
                    UsePartialFiles = true, 
                    WebSeedDelay = TimeSpan.Zero, 
                    WebSeedSpeedTrigger = 0
                }.ToSettings(), factories));
        }

        #endregion

        #region IDisposable API

        private bool disposedValue;

        protected virtual async void Dispose(bool disposing)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task;
            if (!disposedValue)
            {
                if (disposing)
                {
                    dhtEngine.Dispose();
                    httpClient.Dispose();
                    periodicTimer.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
