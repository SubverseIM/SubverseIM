using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public class PeerService : IPeerService
    {
        private readonly INativeService nativeService;

        public PeerService(INativeService nativeService) // TODO: use IPgpKeyProvider service
        {
            this.nativeService = nativeService;
        }

        public SubversePeerId ThisPeer => throw new NotImplementedException();

        public IPEndPoint? LocalEndPoint => throw new NotImplementedException();

        public IPEndPoint? RemoteEndPoint => throw new NotImplementedException();

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers => throw new NotImplementedException();

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
    }
}
