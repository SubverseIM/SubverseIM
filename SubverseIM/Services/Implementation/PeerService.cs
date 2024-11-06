using PgpCore;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class PeerService : IPeerService, IInjectable
    {
        private readonly INativeService nativeService;

        private readonly HttpClient http;

        private readonly Dictionary<SubversePeerId, EncryptionKeys> peerKeysMap;

        public SubversePeerId? ThisPeer { get; private set; }

        public IPEndPoint? LocalEndPoint { get; private set; }

        public IPEndPoint? RemoteEndPoint { get; private set; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public PeerService(INativeService nativeService)
        {
            this.nativeService = nativeService;

            http = new() { BaseAddress = new Uri("https://subverse.network") };

            peerKeysMap = new();

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

        public async Task InjectAsync(IServiceManager serviceManager, CancellationToken cancellationToken)
        {
            (Stream publicKeyStream, Stream privateKeyStream) = 
                GenerateKeysIfNone(await serviceManager.GetWithAwaitAsync<IDbService>());

            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");

            publicKeyStream.Dispose();
            privateKeyStream.Dispose();

            ThisPeer = new(myKeys.PublicKey.GetFingerprint());
            peerKeysMap.Add(ThisPeer.Value, myKeys);
        }

        public Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            // TODO: call Bootstrapper API and populate DHT routing table
        }

        public Task ListenAsync(CancellationToken cancellationToken = default)
        {
            // TODO: listen loop for SIP messages
        }

        public Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            // TODO: wait for message and return from TaskCompletionSource
        }

        public Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
        {
            // TODO: Send outbound message over SIP transport
        }

        public async Task SendInviteAsync(CancellationToken cancellationToken = default)
        {
            string inviteUri = await http.GetFromJsonAsync<string>($"/invite?p={ThisPeer}") ?? 
                throw new InvalidOperationException("Failed to resolve inviteUri!");
            await nativeService.ShareStringToAppAsync("Send Invite Via App", inviteUri);
        }
    }
}
