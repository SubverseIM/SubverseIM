using SubverseIM.Models;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IPeerService
    {
        SubversePeerId? ThisPeer { get; }

        IPEndPoint? LocalEndPoint { get; }

        IPEndPoint? RemoteEndPoint { get; }

        IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        Task BootstrapSelfAsync(IServiceManager serviceManager, CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(IServiceManager serviceManager, CancellationToken cancellationToken = default);

        Task SendMessageAsync(IServiceManager serviceManager, SubverseMessage message, CancellationToken cancellationToken = default);

        Task SendInviteAsync(IServiceManager serviceManager, CancellationToken cancellationToken = default);
    }
}
