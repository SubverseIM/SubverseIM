using SubverseIM.Models;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IPeerService
    {
        SubversePeerId ThisPeer { get; }

        IPEndPoint? LocalEndPoint { get; }

        IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        Task BootstrapSelfAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default);

        Task SendInviteAsync(CancellationToken cancellationToken = default);
    }
}
