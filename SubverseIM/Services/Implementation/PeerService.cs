﻿using Avalonia;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;
using MonoTorrent.PortForwarding;
using PgpCore;
using SIPSorcery.SIP;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class PeerService : IPeerService, IInjectable, IDisposable
    {
        private const int MAGIC_PORT_NUM = 6_03_03;

        private const string MAGIC_SECRET_PASSWORD = "#FreeTheInternet";

        private const string DEFAULT_BOOTSTRAPPER_ROOT = "https://subverse.network";

        private const string PUBLIC_KEY_PATH = "$/pkx/public.key";

        private const string PRIVATE_KEY_PATH = "$/pkx/private.key";

        private const string NODES_LIST_PATH = "$/pkx/nodes.list";

        private readonly INativeService nativeService;

        private readonly TaskCompletionSource<IServiceManager> serviceManagerTcs;

        private readonly TaskCompletionSource<IDhtEngine> dhtEngineTcs;

        private readonly TaskCompletionSource<IDhtListener> dhtListenerTcs;

        private readonly TaskCompletionSource<IPortForwarder> portForwarderTcs;

        private readonly HttpClient http;

        private readonly SIPUDPChannel sipChannel;

        private readonly SIPTransport sipTransport;

        private readonly Dictionary<string, SIPRequest> callIdMap;

        private readonly ConcurrentBag<TaskCompletionSource<IList<PeerInfo>>> peerInfoBag;

        private readonly ConcurrentBag<TaskCompletionSource<SubverseMessage>> messagesBag;

        private readonly PeriodicTimer timer;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        private readonly TaskCompletionSource<SubverseConfig> configTcs;

        public IPEndPoint? LocalEndPoint { get; private set; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public PeerService(INativeService nativeService)
        {
            this.nativeService = nativeService;
            serviceManagerTcs = new();

            dhtEngineTcs = new();
            dhtListenerTcs = new();
            portForwarderTcs = new();

            http = new();

            sipChannel = new SIPUDPChannel(IPAddress.Any, MAGIC_PORT_NUM);
            sipTransport = new SIPTransport(stateless: true);

            thisPeerTcs = new();
            configTcs = new();

            callIdMap = new();
            peerInfoBag = new();
            messagesBag = new();

            timer = new(TimeSpan.FromSeconds(5));

            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
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
                        password: MAGIC_SECRET_PASSWORD
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

        private async Task<EncryptionKeys> GetPeerKeysAsync(SubversePeerId otherPeer, CancellationToken cancellationToken = default)
        {
            SubverseConfig config = await configTcs.Task;
            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubversePeer? peer;
            lock (CachedPeers)
            {
                CachedPeers.TryGetValue(otherPeer, out peer);
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
                            await responseStream.CopyToAsync(publicKeyStream);
                            publicKeyStream.Position = 0;
                        }
                        break;
                    }
                    catch (HttpRequestException) { }
                }

                if (dbService.TryGetReadStream(PRIVATE_KEY_PATH, out Stream? privateKeyStream))
                {
                    peerKeys = new(publicKeyStream, privateKeyStream, MAGIC_SECRET_PASSWORD);
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

        private async Task<bool> SynchronizePeersAsync(Uri bootstrapperUri, CancellationToken cancellationToken = default)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task;

            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            try
            {
                SubversePeer peer;
                SubversePeerId thisPeer = await GetPeerIdAsync(cancellationToken);
                lock (CachedPeers)
                {
                    peer = CachedPeers[thisPeer];
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

        private async Task<bool> SynchronizePeersAsync(Uri bootstrapperUri, SubversePeerId peerId, CancellationToken cancellationToken = default)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task;
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

        private void DhtPeersFound(object? sender, PeersFoundEventArgs e)
        {
            TaskCompletionSource<IList<PeerInfo>>? tcs;
            if (!peerInfoBag.TryTake(out tcs))
            {
                peerInfoBag.Add(tcs = new());
            }

            tcs.TrySetResult(e.Peers);
        }

        private async Task SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubversePeerId fromPeer = SubversePeerId.FromString(sipRequest.Header.From.FromURI.User);
            string fromName = sipRequest.Header.From.FromName;

            SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
            string toName = sipRequest.Header.To.ToName;

            string? messageContent;
            try
            {
                using (PGP pgp = new PGP(await GetPeerKeysAsync(fromPeer)))
                using (MemoryStream encryptedMessageStream = new(sipRequest.BodyBuffer))
                using (MemoryStream decryptedMessageStream = new())
                {
                    await pgp.DecryptAndVerifyAsync(encryptedMessageStream, decryptedMessageStream);
                    messageContent = Encoding.UTF8.GetString(decryptedMessageStream.ToArray());
                }
            }
            catch
            {
                messageContent = sipRequest.Body;
            }

            IEnumerable<SubversePeerId> recipients = [toPeer, ..sipRequest.Header.Contact
                        .Select(x => SubversePeerId.FromString(x.ContactURI.User))];

            IEnumerable<string?> localRecipientNames = recipients
                .Select(x => dbService.GetContact(x)?.DisplayName);

            IEnumerable<string> remoteRecipientNames =
                [toName, .. sipRequest.Header.Contact.Select(x => x.ContactName)];

            SubverseMessage message = new SubverseMessage
            {
                CallId = sipRequest.Header.CallId,
                Content = messageContent,
                Sender = fromPeer,
                SenderName = fromName,
                Recipients = recipients.ToArray(),
                RecipientNames = localRecipientNames
                    .Zip(remoteRecipientNames)
                    .Select(x => x.First ?? x.Second)
                    .ToArray(),
                DateSignedOn = DateTime.Parse(sipRequest.Header.Date),
                TopicName = sipRequest.URI.Parameters.Get("topic"),
            };

            SubversePeer? peer;
            lock (CachedPeers)
            {
                if (!CachedPeers.TryGetValue(fromPeer, out peer))
                {
                    CachedPeers.Add(fromPeer, peer = new() { OtherPeer = fromPeer });
                }
            }
            peer.RemoteEndPoint = remoteEndPoint.GetIPEndPoint();

            bool hasReachedDestination = toPeer == await GetPeerIdAsync();
            message.WasDecrypted = message.WasDelivered = hasReachedDestination;
            if (hasReachedDestination)
            {
                if (!messagesBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
                {
                    messagesBag.Add(tcs = new());
                }
                tcs.SetResult(message);

                SIPResponse sipResponse = SIPResponse.GetResponse(
                    sipRequest, SIPResponseStatusCodesEnum.Ok, "Message was delivered."
                    );
                await sipTransport.SendResponseAsync(remoteEndPoint, sipResponse);
            }
            else
            {
                dbService.InsertOrUpdateItem(message);
                await SendSIPRequestAsync(sipRequest);

                SIPResponse sipResponse = SIPResponse.GetResponse(
                    sipRequest, SIPResponseStatusCodesEnum.Accepted, "Message was forwarded."
                    );
                await sipTransport.SendResponseAsync(remoteEndPoint, sipResponse);
            }
        }

        private async Task SIPTransportResponseReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubversePeerId peerId;
            lock (callIdMap)
            {
                if (!callIdMap.Remove(sipResponse.Header.CallId, out SIPRequest? sipRequest))
                {
                    throw new InvalidOperationException("Received response for invalid Call ID!");
                }
                else
                {
                    peerId = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
                }
            }

            if (sipResponse.Status == SIPResponseStatusCodesEnum.Ok)
            {
                lock (CachedPeers)
                {
                    if (CachedPeers.TryGetValue(peerId, out SubversePeer? peer))
                    {
                        peer.RemoteEndPoint = remoteEndPoint.GetIPEndPoint();
                    }
                }
            }

            SubverseMessage? message = dbService.GetMessageByCallId(sipResponse.Header.CallId);
            if (message is not null)
            {
                message.WasDelivered = true;
                dbService.InsertOrUpdateItem(message);
            }
        }

        private async Task SendSIPRequestAsync(SIPRequest sipRequest, CancellationToken cancellationToken = default)
        {
            IDhtEngine dhtEngine = await dhtEngineTcs.Task;

            SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.URI.User);
            IPEndPoint? cachedEndPoint;
            lock (CachedPeers)
            {
                CachedPeers.TryGetValue(toPeer, out SubversePeer? peer);
                cachedEndPoint = peer?.RemoteEndPoint;
            }

            if (cachedEndPoint is not null)
            {
                await sipTransport.SendRequestAsync(new(cachedEndPoint), sipRequest);
            }

            dhtEngine.GetPeers(new(toPeer.GetBytes()));

            TaskCompletionSource<IList<PeerInfo>>? peerInfoTcs;
            if (!peerInfoBag.TryTake(out peerInfoTcs))
            {
                peerInfoBag.Add(peerInfoTcs = new());
            }

            IList<PeerInfo> peerInfo = await peerInfoTcs.Task;
            foreach (Uri peerUri in peerInfo.Select(x => x.ConnectionUri))
            {
                if (!IPAddress.TryParse(peerUri.DnsSafeHost, out IPAddress? ipAddress))
                {
                    continue;
                }

                IPEndPoint ipEndPoint = new(ipAddress, peerUri.Port);
                await sipTransport.SendRequestAsync(new(ipEndPoint), sipRequest);
            }
        }

        public async Task InjectAsync(IServiceManager serviceManager)
        {
            // DI container init

            serviceManagerTcs.SetResult(serviceManager);
            serviceManager.GetOrRegister(nativeService);

            // Torrent init

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

            // DB init

            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubverseConfig config = dbService.GetConfig() ?? new SubverseConfig
            { BootstrapperUriList = [DEFAULT_BOOTSTRAPPER_ROOT] };
            configTcs.SetResult(config);

            (Stream publicKeyStream, Stream privateKeyStream) =
                GenerateKeysIfNone(dbService);
            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, MAGIC_SECRET_PASSWORD);

            publicKeyStream.Dispose();
            privateKeyStream.Dispose();

            // Routing init

            SubversePeerId thisPeer = new(myKeys.PublicKey.GetFingerprint());
            thisPeerTcs.SetResult(thisPeer);

            lock (CachedPeers)
            {
                CachedPeers.Add(thisPeer, new SubversePeer
                {
                    OtherPeer = thisPeer,
                    KeyContainer = myKeys
                });
            }

            // DB final init

            dbService.GetMessagesWithPeersOnTopic([thisPeer], null);
        }

        public async Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            SubverseConfig config = await configTcs.Task;

            IDhtEngine dhtEngine = await dhtEngineTcs.Task;
            IDhtListener dhtListener = await dhtListenerTcs.Task;
            IPortForwarder portForwarder = await portForwarderTcs.Task;

            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            // Begin listening for SIP traffic
            LocalEndPoint = sipChannel.ListeningEndPoint;

            sipTransport.SIPTransportRequestReceived += SIPTransportRequestReceived;
            sipTransport.SIPTransportResponseReceived += SIPTransportResponseReceived;
            sipTransport.AddSIPChannel(sipChannel);

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
            Mapping? mapping = portForwarder.Mappings.Created.SingleOrDefault();
            for (portNum = MAGIC_PORT_NUM; retryCount++ < 3 && mapping is null; portNum++)
            {
                if (!portForwarder.Active) break;

                await portForwarder.RegisterMappingAsync(new Mapping(Protocol.Udp, LocalEndPoint.Port, portNum));
                await timer.WaitForNextTickAsync();

                mapping = portForwarder.Mappings.Created.SingleOrDefault();
            }

            // Perform synchronization activities
            while (!cancellationToken.IsCancellationRequested)
            {
                SubversePeerId[] peers;
                lock (CachedPeers)
                {
                    peers = CachedPeers.Keys.ToArray();
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

                foreach (SubversePeerId otherPeer in peers)
                {
                    dhtEngine.Announce(new InfoHash(otherPeer.GetBytes()),
                        mapping?.PublicPort ?? portNum);
                }
            }

            // Shutdown all network traffic
            sipTransport.Shutdown();
            await dhtEngine.StopAsync();
            await portForwarder.StopAsync(default);
        }

        public async Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken = default)
        {
            return await thisPeerTcs.Task.WaitAsync(cancellationToken);
        }

        public async Task<SubverseConfig> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            return await configTcs.Task.WaitAsync(cancellationToken);
        }

        public async Task<bool> PersistConfigAsync(CancellationToken cancellationToken = default)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task;
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

            SubverseConfig config = await GetConfigAsync(cancellationToken);
            dbService.UpdateConfig(config);

            return true;
        }

        public Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            if (!messagesBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
            {
                messagesBag.Add(tcs = new());
            }

            return tcs.Task;
        }

        public async Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
        {
            List<Task> sendTasks = new();
            foreach ((SubversePeerId recipient, string contactName) in message.Recipients.Zip(message.RecipientNames))
            {
                sendTasks.Add(Task.Run(async Task? () =>
                {
                    SIPURI requestUri = SIPURI.ParseSIPURI($"sip:{recipient}@subverse.network");
                    if (message.TopicName is not null)
                    {
                        requestUri.Parameters.Set("topic", message.TopicName);
                    }

                    SIPURI toURI = SIPURI.ParseSIPURI($"sip:{recipient}@subverse.network");
                    SIPURI fromURI = SIPURI.ParseSIPURI($"sip:{message.Sender}@subverse.network");

                    SIPRequest sipRequest = SIPRequest.GetRequest(
                        SIPMethodsEnum.MESSAGE, requestUri,
                        new(contactName, toURI, null),
                        new(message.SenderName, fromURI, null)
                        );

                    if (message.CallId is not null)
                    {
                        sipRequest.Header.CallId = message.CallId;
                    }

                    sipRequest.Header.SetDateHeader();

                    sipRequest.Header.Contact = new();
                    for (int i = 0; i < message.Recipients.Length; i++)
                    {
                        if (message.Recipients[i] == recipient) continue;

                        SIPURI contactUri = SIPURI.ParseSIPURI($"sip:{message.Recipients[i]}@subverse.network");
                        sipRequest.Header.Contact.Add(new(message.RecipientNames[i], contactUri));
                    }

                    if (message.Sender == await GetPeerIdAsync())
                    {
                        using (PGP pgp = new(await GetPeerKeysAsync(recipient, cancellationToken)))
                        {
                            sipRequest.Body = await pgp.EncryptAndSignAsync(message.Content);
                        }
                    }
                    else
                    {
                        sipRequest.Body = message.Content;
                    }

                    lock (callIdMap)
                    {
                        if (!callIdMap.ContainsKey(sipRequest.Header.CallId))
                        {
                            callIdMap.Add(sipRequest.Header.CallId, sipRequest);
                        }
                        else
                        {
                            callIdMap[sipRequest.Header.CallId] = sipRequest;
                        }
                    }

                    bool flag;
                    using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(1500));
                    do
                    {
                        await SendSIPRequestAsync(sipRequest, cancellationToken);
                        await timer.WaitForNextTickAsync(cancellationToken);
                        lock (callIdMap)
                        {
                            flag = callIdMap.ContainsKey(sipRequest.Header.CallId);
                        }
                    } while (flag && !cancellationToken.IsCancellationRequested);
                }));
            }

            await Task.WhenAll(sendTasks);
        }

        public async Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken)
        {
            IServiceManager serviceManager = await serviceManagerTcs.Task;
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();

            SubversePeerId thisPeer = await GetPeerIdAsync(cancellationToken);
            string inviteId = await http.GetFromJsonAsync<string>(new Uri($"{DEFAULT_BOOTSTRAPPER_ROOT}/invite?p={thisPeer}")) ??
                throw new InvalidOperationException("Failed to resolve inviteUri!");

            await launcherService.ShareUrlToAppAsync(sender, "Send Invite Via App", $"{DEFAULT_BOOTSTRAPPER_ROOT}/invite/{inviteId}");
        }

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
                    sipChannel.Dispose();
                    sipTransport.Dispose();
                    timer.Dispose();
                }

                CachedPeers.Clear();
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
