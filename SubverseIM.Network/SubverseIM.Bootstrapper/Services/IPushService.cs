using SIPSorcery.SIP;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IPushService
    {
        Task RegisterPeerAsync(SubversePeerId peerId, string deviceToken, CancellationToken cancellationToken = default);

        Task SendPushNotificationAsync(SIPRequest sipRequest, CancellationToken cancellationToken = default);
    }
}