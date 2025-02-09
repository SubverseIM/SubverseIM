using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using MonoTorrent;
using PgpCore;
using SubverseIM.Core;
using SubverseIM.Models;

namespace SubverseIM.Services;

public interface IBootstrapperService
{
    const string DEFAULT_BOOTSTRAPPER_ROOT = "https://subverse.network";

    const int DEFAULT_PORT_NUMBER = 6_03_03;

    Task BootstrapSelfAsync(CancellationToken cancellationToken = default);

    Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken = default);

    Task<IList<PeerInfo>> GetPeerInfoAsync(SubversePeerId toPeer, CancellationToken cancellationToken = default);

    Task<EncryptionKeys> GetPeerKeysAsync(SubversePeerId otherPeer, CancellationToken cancellationToken = default);

    Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken = default);
}
