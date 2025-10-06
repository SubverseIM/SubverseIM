using SIPSorcery.SIP;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IPushService
    {
        Task RegisterPeerAsync(SubversePeerId peerId, string deviceToken, CancellationToken cancellationToken = default);

        Task SendPushNotificationAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken = default);

        bool TryStoreMessage(SIPMessageBase sipMessage);
    }
}