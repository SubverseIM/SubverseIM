using Fitomad.Apns;
using Fitomad.Apns.Entities.Notification;
using Microsoft.Extensions.Caching.Distributed;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public class PushService : IPushService
    {
        private readonly IDistributedCache _cache;

        private readonly IApnsClient? _apnsClient;

        public PushService(IDistributedCache cache, IApnsClient apnsClient)
        {
            _cache = cache;
            _apnsClient = apnsClient;
        }

        public PushService(IDistributedCache cache)
        {
            _cache = cache;
            _apnsClient = null;
        }

        public Task RegisterPeerAsync(SubversePeerId peerId, string deviceToken, CancellationToken cancellationToken)
        {
            return _cache.SetStringAsync($"TKN-{peerId}", deviceToken, cancellationToken);
        }

        public async Task SendPushNotificationAsync(SubversePeerId peerId, CancellationToken cancellationToken)
        {
            string? deviceToken;
            if (_apnsClient is null)
            {
                return;
            }
            else
            {
                deviceToken = await _cache.GetStringAsync($"TKN-{peerId}", cancellationToken);
            }

            if (!string.IsNullOrEmpty(deviceToken))
            {
                Notification notification = new NotificationBuilder()
                    .WithAlert(new Alert()
                    {
                        Title = "Network Updated",
                        Subtitle = "You have new messages!",
                        Body = "Tap this notification to see more details."
                    })
                    .PlaySound("notifMessage.aif")
                    .Build();

                cancellationToken.ThrowIfCancellationRequested();
                await _apnsClient.SendAsync(notification, deviceToken);
            }
        }
    }
}
