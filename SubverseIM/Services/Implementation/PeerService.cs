using MonoTorrent;
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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class PeerService : IPeerService, IInjectable
    {
        private readonly INativeService nativeService;

        private readonly IDhtEngine dhtEngine;

        private readonly IDhtListener dhtListener;

        private readonly IPortForwarder portForwarder;

        private readonly HttpClient http;

        private readonly SIPUDPChannel sipChannel;

        private readonly SIPTransport sipTransport;

        private readonly Dictionary<string, SubversePeerId> callIdMap;

        private readonly Dictionary<SubversePeerId, TaskCompletionSource<IList<PeerInfo>>> peerInfoMap;

        private readonly ConcurrentBag<TaskCompletionSource<SubverseMessage>> messagesBag;

        private readonly PeriodicTimer timer;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        private readonly TaskCompletionSource<IDbService> dbServiceTcs;
        private IDbService DbService => dbServiceTcs.Task.Result;

        private readonly TaskCompletionSource<ILauncherService> launcherServiceTcs;
        private ILauncherService LauncherService => launcherServiceTcs.Task.Result;

        public IPEndPoint? LocalEndPoint { get; private set; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public SubversePeerId ThisPeer => thisPeerTcs.Task.Result;

        public PeerService(INativeService nativeService)
        {
            this.nativeService = nativeService;

            dhtEngine = new DhtEngine();
            dhtListener = new DhtListener(new IPEndPoint(IPAddress.Any, 0));

            http = new() { BaseAddress = new Uri("https://subverse.network/") };
            portForwarder = new MonoNatPortForwarder();

            sipChannel = new SIPUDPChannel(IPAddress.Any, 0);
            sipTransport = new SIPTransport(stateless: true);

            thisPeerTcs = new();

            dbServiceTcs = new();
            launcherServiceTcs = new();

            callIdMap = new();
            peerInfoMap = new();
            messagesBag = new();

            timer = new(TimeSpan.FromSeconds(15));

            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
        }

        private (Stream, Stream) GenerateKeysIfNone(IDbService dbService)
        {
            if (dbService.TryGetReadStream("$/pkx/public.key", out Stream? publicKeyStream) &&
                dbService.TryGetReadStream("$/pkx/private.key", out Stream? privateKeyStream))
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
                        password: "#FreeTheInternet"
                        );
                }

                using (Stream publicKeyStoreStream = dbService.CreateWriteStream("$/pkx/public.key"))
                using (Stream privateKeyStoreStream = dbService.CreateWriteStream("$/pkx/private.key"))
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
            EncryptionKeys? peerKeys;
            if (CachedPeers.TryGetValue(otherPeer, out SubversePeer? peer) && peer.KeyContainer is null)
            {
                Stream publicKeyStream = await http.GetStreamAsync($"pk?p={otherPeer}", cancellationToken);
                if (DbService.TryGetReadStream("$/pkx/private.key", out Stream? privateKeyStream))
                {
                    peerKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");
                    peer.KeyContainer = peerKeys;

                    publicKeyStream.Dispose();
                    privateKeyStream.Dispose();
                }
                else
                {
                    throw new InvalidOperationException("Could not find private key file in application database!");
                }
            }
            else if (peer?.KeyContainer is not null)
            {
                peerKeys = peer.KeyContainer;
            }
            else
            {
                throw new InvalidOperationException($"Could not find public key for Peer ID: {otherPeer}");
            }

            return peerKeys;
        }

        private async Task<bool> SynchronizePeersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                ReadOnlyMemory<byte> nodesBytes = await dhtEngine.SaveNodesAsync();

                byte[] requestBytes;
                using (PGP pgp = new(CachedPeers[ThisPeer].KeyContainer))
                using (MemoryStream inputStream = new(nodesBytes.ToArray()))
                using (MemoryStream outputStream = new())
                {
                    await pgp.SignAsync(inputStream, outputStream);
                    requestBytes = outputStream.ToArray();
                }

                using (ByteArrayContent requestContent = new(requestBytes)
                { Headers = { ContentType = new("application/octet-stream") } })
                {
                    HttpResponseMessage response = await http.PostAsync($"nodes?p={ThisPeer}", requestContent, cancellationToken);
                    return await response.Content.ReadFromJsonAsync<bool>(cancellationToken);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task SynchronizePeersAsync(SubversePeerId peerId, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await http.GetAsync($"nodes?p={peerId}", cancellationToken);
            byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            dhtEngine.Add([responseBytes]);
        }

        private void DhtPeersFound(object? sender, PeersFoundEventArgs e)
        {
            TaskCompletionSource<IList<PeerInfo>>? tcs;
            lock (peerInfoMap)
            {
                SubversePeerId otherPeer = new(e.InfoHash.Span);
                if (!peerInfoMap.Remove(otherPeer, out tcs))
                {
                    peerInfoMap.Add(otherPeer, tcs = new());
                }
            }
            tcs.SetResult(e.Peers);
        }

        private async Task SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            SubversePeerId fromPeer = SubversePeerId.FromString(sipRequest.Header.From.FromURI.User);
            SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);

            if (toPeer != ThisPeer)
            {
                await SendSIPRequestAsync(sipRequest);
                SIPResponse sipResponse = SIPResponse.GetResponse(
                    sipRequest, SIPResponseStatusCodesEnum.Accepted, "Message was forwarded."
                    );
                await sipTransport.SendResponseAsync(remoteEndPoint, sipResponse);
            }
            else
            {
                SubversePeer? peer;
                lock (CachedPeers)
                {
                    if (!CachedPeers.TryGetValue(fromPeer, out peer))
                    {
                        CachedPeers.Add(fromPeer, peer = new() { OtherPeer = fromPeer });
                    }
                }
                peer.RemoteEndPoint = remoteEndPoint.GetIPEndPoint();

                string messageContent;
                using (PGP pgp = new PGP(await GetPeerKeysAsync(fromPeer)))
                using (MemoryStream encryptedMessageStream = new(sipRequest.BodyBuffer))
                using (MemoryStream decryptedMessageStream = new())
                {
                    await pgp.DecryptAndVerifyAsync(encryptedMessageStream, decryptedMessageStream);
                    messageContent = Encoding.UTF8.GetString(decryptedMessageStream.ToArray());
                }

                if (!messagesBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
                {
                    messagesBag.Add(tcs = new());
                }

                tcs.SetResult(new SubverseMessage
                {
                    Content = messageContent,
                    Sender = fromPeer,
                    Recipient = toPeer,
                    DateSignedOn = DateTime.Parse(sipRequest.Header.Date)
                });

                SIPResponse sipResponse = SIPResponse.GetResponse(
                    sipRequest, SIPResponseStatusCodesEnum.Ok, "Message was delivered."
                    );
                await sipTransport.SendResponseAsync(remoteEndPoint, sipResponse);
            }
        }

        private Task SIPTransportResponseReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse)
        {
            if (sipResponse.Status == SIPResponseStatusCodesEnum.Ok)
            {
                SubversePeerId peerId;
                lock (callIdMap)
                {
                    if (!callIdMap.TryGetValue(sipResponse.Header.CallId, out peerId))
                    {
                        throw new InvalidOperationException("Received response for invalid Call ID!");
                    }
                }

                lock (CachedPeers)
                {
                    if (CachedPeers.TryGetValue(peerId, out SubversePeer? peer))
                    {
                        peer.RemoteEndPoint = remoteEndPoint.GetIPEndPoint();
                    }
                }
            }

            return Task.CompletedTask;
        }

        private async Task SendSIPRequestAsync(SIPRequest sipRequest, CancellationToken cancellationToken = default)
        {
            SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
            lock (callIdMap)
            {
                if (!callIdMap.ContainsKey(sipRequest.Header.CallId))
                {
                    callIdMap.Add(sipRequest.Header.CallId, toPeer);
                }
                else
                {
                    callIdMap[sipRequest.Header.CallId] = toPeer;
                }
            }

            TaskCompletionSource<IList<PeerInfo>>? peerInfoTcs;
            lock (peerInfoMap)
            {
                if (!peerInfoMap.Remove(toPeer, out peerInfoTcs))
                {
                    peerInfoMap.Add(toPeer, peerInfoTcs = new());
                }
            }

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

        public async Task InjectAsync(IServiceManager serviceManager, CancellationToken cancellationToken)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            (Stream publicKeyStream, Stream privateKeyStream) =
                GenerateKeysIfNone(dbService);
            dbServiceTcs.SetResult(dbService);

            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            launcherServiceTcs.SetResult(launcherService);

            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");

            publicKeyStream.Dispose();
            privateKeyStream.Dispose();

            thisPeerTcs.SetResult(new(myKeys.PublicKey.GetFingerprint()));

            lock (CachedPeers)
            {
                CachedPeers.Add(ThisPeer, new SubversePeer
                {
                    OtherPeer = ThisPeer,
                    KeyContainer = myKeys
                });
            }
        }

        public async Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            LocalEndPoint = sipChannel.ListeningEndPoint;

            sipTransport.SIPTransportRequestReceived += SIPTransportRequestReceived;
            sipTransport.SIPTransportResponseReceived += SIPTransportResponseReceived;

            dhtEngine.PeersFound += DhtPeersFound;

            await dhtEngine.SetListenerAsync(dhtListener);
            await dhtEngine.StartAsync();

            await portForwarder.StartAsync(cancellationToken);
            await portForwarder.RegisterMappingAsync(new Mapping(Protocol.Udp, LocalEndPoint.Port, 
                RandomNumberGenerator.GetInt32(1024, ushort.MaxValue)));

            if (DbService.TryGetReadStream("$/pkx/public.key", out Stream? pkStream)) 
            {
                using (pkStream)
                using (StreamContent pkStreamContent = new(pkStream)
                { Headers = { ContentType = new("application/pgp-keys") } })
                {
                    await http.PostAsync("pk", pkStreamContent, cancellationToken);
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                if (portForwarder.Mappings.Created.Count > 0)
                {
                    dhtEngine.Announce(new(ThisPeer.GetBytes()),
                        portForwarder.Mappings.Created[0].PublicPort);
                }

                await SynchronizePeersAsync(cancellationToken);
                await timer.WaitForNextTickAsync(cancellationToken);

                SubversePeerId[] peers;
                lock (CachedPeers)
                {
                    peers = CachedPeers.Keys.ToArray();
                }

                foreach (SubversePeerId peer in peers)
                {
                    await SynchronizePeersAsync(peer, cancellationToken);
                    await timer.WaitForNextTickAsync(cancellationToken);
                }

            }

            await dhtEngine.StopAsync();
            sipTransport.Shutdown();
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
            SIPURI requestFromUri = SIPURI.ParseSIPURI($"im:{message.Sender}@subverse.network");
            SIPURI requestToUri = SIPURI.ParseSIPURI($"im:{message.Recipient}@subverse.network");

            SIPRequest sipRequest = SIPRequest.GetRequest(
                SIPMethodsEnum.MESSAGE, requestToUri,
                new SIPToHeader(string.Empty, requestToUri, string.Empty),
                new SIPFromHeader(string.Empty, requestFromUri, string.Empty)
                );

            sipRequest.Header.SetDateHeader();

            using (PGP pgp = new(await GetPeerKeysAsync(message.Recipient, cancellationToken)))
            {
                sipRequest.Body = await pgp.EncryptAndSignAsync(message.Content);
            }

            await SendSIPRequestAsync(sipRequest, cancellationToken);
        }

        public async Task SendInviteAsync(CancellationToken cancellationToken = default)
        {
            string inviteId = await http.GetFromJsonAsync<string>($"invite?p={ThisPeer}") ??
                throw new InvalidOperationException("Failed to resolve inviteUri!");
            await LauncherService.ShareStringToAppAsync("Send Invite Via App", $"https://subverse.network/invite/{inviteId}");
        }
    }
}
