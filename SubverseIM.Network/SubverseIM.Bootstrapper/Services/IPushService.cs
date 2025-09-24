using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IPushService
    {
        Task RegisterPeerAsync(SubversePeerId peerId, string deviceToken, CancellationToken cancellationToken = default);

        Task SendPushNotificationAsync(SubversePeerId peerId, CancellationToken cancellationToken = default);
    }
}