using SIPSorcery.SIP;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IPushService
    {
        Task RegisterPeerAsync(SubversePeerId peerId, byte[] deviceToken, CancellationToken cancellationToken = default);

        Task SendPushNotificationAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken = default);

        Task<bool> TryStoreMessageAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken = default);
    }
}