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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SubverseIM.Services.Implementation
{
    public class BootstrapperService : IBootstrapperService, IDisposableService, IInjectable
    {
        private const string SECRET_PASSWORD = "#FreeTheInternet";

        private const string PUBLIC_KEY_PATH = "$/pkx/public.key";

        private const string PRIVATE_KEY_PATH = "$/pkx/private.key";

        private const string NODES_LIST_PATH = "$/pkx/nodes.list";

        private static readonly IReadOnlyDictionary<string, TimeSpan> expireTimes =
            new Dictionary<string, TimeSpan>()
            {
                { "90 minutes", TimeSpan.FromMinutes(90) },
                { "24 hours", TimeSpan.FromHours(24) },
                { "3 days", TimeSpan.FromDays(3) }
            };

        private readonly INativeService nativeService;

        private readonly HttpClient http;

        private readonly ConcurrentBag<TaskCompletionSource<IList<PeerInfo>>> peerInfoBag;

        private readonly TaskCompletionSource<IDhtEngine> dhtEngineTcs;

        private readonly TaskCompletionSource<IDhtListener> dhtListenerTcs;

        private readonly TaskCompletionSource<IPortForwarder> portForwarderTcs;

        private readonly TaskCompletionSource<IServiceManager> serviceManagerTcs;

        private readonly PeriodicTimer timer;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        public BootstrapperService(INativeService nativeService)
        {
            this.nativeService = nativeService;

            http = new(nativeService.GetNativeHttpHandlerInstance());

            peerInfoBag = new();

            dhtEngineTcs = new();
            dhtListenerTcs = new();
            portForwarderTcs = new();
            serviceManagerTcs = new();

            thisPeerTcs = new();
            timer = new(TimeSpan.FromSeconds(5));
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

        private (Stream, Stream) GenerateKeysIfNone(IDbService dbService)
        {
            if (dbService.TryGetReadStream(PUBLIC_KEY_PATH, out Stream? publicKeyStream) &&
                dbService.TryGetReadStream(PRIVATE_KEY_PATH, out Stream? privateKeyStream))
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
                        password: SECRET_PASSWORD
                        );
                }

                using (Stream publicKeyStoreStream = dbService.CreateWriteStream(PUBLIC_KEY_PATH))
                using (Stream privateKeyStoreStream = dbService.CreateWriteStream(PRIVATE_KEY_PATH))
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
                using (Stream cacheStream = dbService.CreateWriteStream(NODES_LIST_PATH))
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

                using (ByteArrayContent requestContent = new(requestBytes)
                { Headers = { ContentType = new("application/octet-stream") } })
                {
                    HttpResponseMessage response = await http.PostAsync(new Uri(bootstrapperUri, $"nodes?p={thisPeer}"), requestContent, cancellationToken);
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
                HttpResponseMessage response = await http.GetAsync(new Uri(bootstrapperUri, $"nodes?p={peerId}"), cancellationToken);
                byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                dhtEngine.Add([responseBytes]);

                return true;
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
                if (dbService.TryGetReadStream(NODES_LIST_PATH, out Stream? cacheStream))
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
            if (dbService.TryGetReadStream(PUBLIC_KEY_PATH, out Stream? pkStream))
            {
                foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
                {
                    using (pkStream)
                    using (StreamContent pkStreamContent = new(pkStream)
                    { Headers = { ContentType = new("application/pgp-keys") } })
                    {
                        await http.PostAsync(new Uri(bootstrapperUri, "pk"), pkStreamContent, cancellationToken);
                    }
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
                await timer.WaitForNextTickAsync(cancellationToken);

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

                foreach (SubversePeerId otherPeer in peers)
                {
                    await timer.WaitForNextTickAsync(cancellationToken);
                    dhtEngine.Announce(new InfoHash(otherPeer.GetBytes()),
                        mapping?.PublicPort ?? portNum);
                }

                foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
                {
                    if (await SynchronizePeersAsync(bootstrapperUri, cancellationToken))
                    {
                        await timer.WaitForNextTickAsync(cancellationToken);
                    }

                    foreach (SubversePeerId otherPeer in peers)
                    {
                        if (await SynchronizePeersAsync(bootstrapperUri, otherPeer, cancellationToken))
                        {
                            await timer.WaitForNextTickAsync(cancellationToken);
                        }
                    }
                }
            }

            // Shutdown all bootstrapping traffic
            await dhtEngine.StopAsync();
            await portForwarder.StopAsync(true, default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken)
        {
            return await thisPeerTcs.Task.WaitAsync(cancellationToken);
        }

        public async Task<IList<PeerInfo>> GetPeerInfoAsync(SubversePeerId toPeer, CancellationToken cancellationToken)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task.WaitAsync(cancellationToken);
            dhtEngine.GetPeers(new InfoHash(toPeer.GetBytes()));
            if (!peerInfoBag.TryTake(out TaskCompletionSource<IList<PeerInfo>>? tcs))
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
                        using (Stream responseStream = await http.GetStreamAsync(new Uri(bootstrapperUri, $"pk?p={otherPeer}"), cancellationToken))
                        {
                            await responseStream.CopyToAsync(publicKeyStream, cancellationToken);
                            publicKeyStream.Position = 0;
                        }
                        break;
                    }
                    catch (HttpRequestException) { }
                }

                if (dbService.TryGetReadStream(PRIVATE_KEY_PATH, out Stream? privateKeyStream))
                {
                    peerKeys = new(publicKeyStream, privateKeyStream, SECRET_PASSWORD);
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

        public async Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task.WaitAsync(cancellationToken);
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);

            string[] pickerItems = expireTimes.Keys.ToArray();
            string? expireTimeKey = await launcherService.ShowPickerDialogAsync("Link should expire in...", pickerItems[0], pickerItems);
            if (expireTimeKey is null || !expireTimes.TryGetValue(expireTimeKey, out TimeSpan expireTimeValue)) return;

            SubversePeerId thisPeer = await GetPeerIdAsync(cancellationToken);
            UriBuilder builder = new($"{IBootstrapperService.DEFAULT_BOOTSTRAPPER_ROOT}/invite") 
            { 
                Query = $"?p={thisPeer}&t={HttpUtility.UrlEncode(expireTimeValue.TotalHours.ToString())}"
            };
            string inviteId = await http.GetFromJsonAsync<string>(builder.Uri, cancellationToken) ??
                throw new InvalidOperationException("Failed to resolve inviteId!");

            await launcherService.ShareUrlToAppAsync(sender, "Send Invite Via App", $"{IBootstrapperService.DEFAULT_BOOTSTRAPPER_ROOT}/invite/{inviteId}");
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
            dbService.GetMessagesWithPeersOnTopic([], null);

            (Stream publicKeyStream, Stream privateKeyStream) =
                GenerateKeysIfNone(dbService);
            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, SECRET_PASSWORD);

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

            // Torrent service init

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
            string cacheDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrent");
            serviceManager.GetOrRegister<ITorrentService>(
                new TorrentService(serviceManager, new EngineSettingsBuilder
                { CacheDirectory = cacheDirPath, UsePartialFiles = true }.ToSettings(), factories
                ));
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
                    http.Dispose();
                    timer.Dispose();
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
