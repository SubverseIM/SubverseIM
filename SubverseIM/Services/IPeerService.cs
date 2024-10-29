using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IPeerService
    {
        IPEndPoint? LocalEndPoint { get; }

        IPEndPoint? RemoteEndPoint { get; }

        IDictionary<SubversePeerId, IPEndPoint?> CachedPeers { get; }

        Task BootstrapSelfAsync(CancellationToken cancellationToken = default);

        Task ListenAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default);
    }
}
