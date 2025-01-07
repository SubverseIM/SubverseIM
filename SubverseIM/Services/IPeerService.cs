using Avalonia;
using Avalonia.Platform.Storage;
using MonoTorrent;
using MonoTorrent.Dht;
using SubverseIM.Models;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IPeerService
    {
        IPEndPoint? LocalEndPoint { get; }

        IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        Task BootstrapSelfAsync(CancellationToken cancellationToken = default);

        Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default);

        Task SendInviteAsync(Visual? sender = null, CancellationToken cancellationToken = default);
    }
}
