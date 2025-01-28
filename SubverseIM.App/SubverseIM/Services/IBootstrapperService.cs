using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using MonoTorrent;
using PgpCore;
using SubverseIM.Models;

namespace SubverseIM.Services;

public interface IBootstrapperService
{
    Task BootstrapSelfAsync(CancellationToken cancellationToken = default);

    Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken = default);

    Task<IList<PeerInfo>> GetPeerInfoAsync(SubversePeerId toPeer, CancellationToken cancellationToken = default);

    Task<EncryptionKeys> GetPeerKeysAsync(SubversePeerId otherPeer, CancellationToken cancellationToken = default);

    Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken = default);
}
