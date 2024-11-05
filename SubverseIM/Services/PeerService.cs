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
    public class PeerService : IPeerService
    {
        private readonly INativeService nativeService;

        private readonly IDbService dbService;

        private readonly HttpClient http;

        private readonly Dictionary<SubversePeerId, EncryptionKeys> peerKeysMap;

        public SubversePeerId ThisPeer { get; }

        public IPEndPoint? LocalEndPoint { get; private set; }

        public IPEndPoint? RemoteEndPoint { get; private set; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public PeerService(IDbService dbService, INativeService nativeService)
        {
            this.dbService = dbService;
            this.nativeService = nativeService;

            http = new() { BaseAddress = new Uri("https://subverse.network") };

            (Stream publicKeyStream, Stream privateKeyStream) = GenerateKeysIfNone();
            EncryptionKeys myKeys = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");

            ThisPeer = new(myKeys.PublicKey.GetFingerprint());
            peerKeysMap = new() { { ThisPeer, myKeys } };

            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
        }

        private (Stream, Stream) GenerateKeysIfNone()
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

        public Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task ListenAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task SendInviteAsync(CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await http.PostAsync($"/invite?p={ThisPeer}", new ByteArrayContent([]));
            string? inviteUri = await response.Content.ReadFromJsonAsync<string?>();
            await nativeService.ShareStringToAppAsync("Send Invite Via App", inviteUri ?? string.Empty);
        }
    }
}
