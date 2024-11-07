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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class PeerService : IPeerService, IInjectable
    {
        private readonly IDhtEngine dhtEngine;

        private readonly IDhtListener dhtListener;

        private readonly IPortForwarder portForwarder;

        private readonly INativeService nativeService;

        private readonly HttpClient http;

        private readonly SIPUDPChannel sipChannel;

        private readonly SIPTransport sipTransport;

        private readonly Dictionary<SubversePeerId, EncryptionKeys> peerKeysMap;

        private readonly Dictionary<SubversePeerId, TaskCompletionSource<IList<PeerInfo>>> peerInfoMap;

        private readonly ConcurrentBag<TaskCompletionSource<SubverseMessage>> messagesBag;

        private readonly PeriodicTimer timer;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        private readonly TaskCompletionSource<IDbService> dbServiceTcs;

        public IPEndPoint? LocalEndPoint { get; private set; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public SubversePeerId ThisPeer => thisPeerTcs.Task.Result;

        public PeerService(INativeService nativeService)
        {
            dhtEngine = new DhtEngine();
            dhtListener = new DhtListener(new IPEndPoint(IPAddress.Any, 0));

            http = new() { BaseAddress = new Uri("https://subverse.network/") };
            portForwarder = new MonoNatPortForwarder();

            sipChannel = new SIPUDPChannel(IPAddress.Any, 0);
            sipTransport = new SIPTransport(stateless: true);

            thisPeerTcs = new();
            dbServiceTcs = new();

            peerKeysMap = new();
            peerInfoMap = new();
            messagesBag = new();

            timer = new(TimeSpan.FromSeconds(15));

            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();

            this.nativeService = nativeService;
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

        private async Task<bool> SynchronizePeersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                ReadOnlyMemory<byte> nodesBytes = await dhtEngine.SaveNodesAsync();

                byte[] requestBytes;
                using (PGP pgp = new(peerKeysMap[ThisPeer]))
                using (MemoryStream inputStream = new(nodesBytes.ToArray()))
                using (MemoryStream outputStream = new())
                {
                    await pgp.SignAsync(inputStream, outputStream);
                    requestBytes = outputStream.ToArray();
                }

                using (ByteArrayContent requestContent = new(requestBytes)
                { Headers = { ContentType = new("application/octet-stream") } })
                {
                    HttpResponseMessage response = await http.PostAsync($"nodes?p={ThisPeer}", requestContent);
                    return await response.Content.ReadFromJsonAsync<bool>();
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

        private async Task SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            SubversePeerId fromPeer = SubversePeerId.FromString(sipRequest.Header.From.FromURI.User);
            SubversePeerId toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);

            if (toPeer == ThisPeer)
            {
                EncryptionKeys? peerKeys;
                if (!peerKeysMap.TryGetValue(fromPeer, out peerKeys))
                {
                    Stream publicKeyStream = await http.GetStreamAsync($"pk?p={fromPeer}");
                    if ((await dbServiceTcs.Task).TryGetReadStream("$/pkx/private.key", out Stream? privateKeyStream))
                    {
                        peerKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");

                        publicKeyStream.Dispose();
                        privateKeyStream.Dispose();
                    }
                    else
                    {
                        throw new InvalidOperationException("Could not find private key file in application database!");
                    }
                }

                string messageContent;
                using (PGP pgp = new PGP(peerKeys))
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
            }
        }

        private Task SIPTransportResponseReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse)
        {
            return Task.CompletedTask;
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

        public async Task InjectAsync(IServiceManager serviceManager, CancellationToken cancellationToken)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            (Stream publicKeyStream, Stream privateKeyStream) =
                GenerateKeysIfNone(dbService);
            dbServiceTcs.SetResult(dbService);

            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");

            publicKeyStream.Dispose();
            privateKeyStream.Dispose();

            thisPeerTcs.SetResult(new(myKeys.PublicKey.GetFingerprint()));

            lock (peerKeysMap)
            {
                peerKeysMap.Add(ThisPeer, myKeys);
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
            await portForwarder.RegisterMappingAsync(new Mapping(Protocol.Udp, LocalEndPoint.Port, 0));

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

            TaskCompletionSource<IList<PeerInfo>>? peerInfoTcs;
            lock (peerInfoMap)
            {
                if (!peerInfoMap.Remove(message.Recipient, out peerInfoTcs))
                {
                    peerInfoMap.Add(message.Recipient, peerInfoTcs = new());
                }
            }

            IList<PeerInfo> peerInfo = await peerInfoTcs.Task;
            foreach (PeerInfo peer in peerInfo) 
            {
                await sipTransport.SendRequestAsync(, sipRequest);
            }
        }

        public async Task SendInviteAsync(CancellationToken cancellationToken = default)
        {
            string inviteUri = await http.GetFromJsonAsync<string>($"invite?p={ThisPeer}") ??
                throw new InvalidOperationException("Failed to resolve inviteUri!");
            await nativeService.ShareStringToAppAsync("Send Invite Via App", inviteUri);
        }
    }
}
