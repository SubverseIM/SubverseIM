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

namespace SubverseIM.Services
{
    public class PeerService : IPeerService, IInjectableService
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
            Stream? publicKeyStream = dbService.GetStream("$/pkx/public.key"),
                privateKeyStream = dbService.GetStream("$/pkx/private.key");
            if (publicKeyStream is null & privateKeyStream is null)
            {
                using (publicKeyStream = dbService.CreateStream("$/pkx/public.key"))
                using (privateKeyStream = dbService.CreateStream("$/pkx/private.key"))
                {
                    using PGP pgp = new();
                    pgp.GenerateKey(
                        publicKeyStream,
                        privateKeyStream,
                        password: "#FreeTheInternet"
                        );
                }

                return (
                    dbService.GetStream("$/pkx/public.key")!, 
                    dbService.GetStream("$/pkx/private.key")!
                    );
            }
            else 
            {
                return (publicKeyStream!, privateKeyStream!);
            }
        }

        public async Task InjectAsync(IServiceManager serviceManager, CancellationToken cancellationToken)
        {
            (Stream publicKeyStream, Stream privateKeyStream) = GenerateKeysIfNone(
                await serviceManager.GetWithAwaitAsync<IDbService>());

            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");

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
            HttpResponseMessage response = await http.PostAsync($"/invite?p={ThisPeer}", new ByteArrayContent([]));
            string? inviteUri = await response.Content.ReadFromJsonAsync<string?>();
            await nativeService.ShareStringToAppAsync("Send Invite Via App", inviteUri ?? string.Empty);
        }
    }
}
