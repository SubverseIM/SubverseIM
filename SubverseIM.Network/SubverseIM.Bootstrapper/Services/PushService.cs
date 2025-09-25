using Fitomad.Apns;
using Fitomad.Apns.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using SIPSorcery.SIP;
using SubverseIM.Bootstrapper.Models;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public class PushService : IPushService
    {
        private readonly SubverseContext _context;

        private readonly IDistributedCache _cache;

        private readonly IApnsClient? _apnsClient;

        public PushService(SubverseContext context, IDistributedCache cache, IApnsClient apnsClient)
        {
            _context = context;
            _cache = cache;
            _apnsClient = apnsClient;
        }

        public PushService(SubverseContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
            _apnsClient = null;
        }

        public Task RegisterPeerAsync(SubversePeerId peerId, string deviceToken, CancellationToken cancellationToken)
        {
            return _cache.SetStringAsync($"TKN-{peerId}", deviceToken, cancellationToken);
        }

        public async Task SendPushNotificationAsync(SIPRequest sipRequest, CancellationToken cancellationToken)
        {
            SubversePeerId toPeer;
            string? deviceToken;

            if (_apnsClient is null)
            {
                return;
            }
            else
            {
                toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
                deviceToken = await _cache.GetStringAsync($"TKN-{toPeer}", cancellationToken);
            }

            if (!string.IsNullOrEmpty(deviceToken))
            {
                await _context.Messages.AddAsync(new SubverseMessage
                { CallId = sipRequest.Header.CallId, OtherPeer = toPeer },
                cancellationToken);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    return;
                }

                Notification notification = new NotificationBuilder()
                    .WithAlert(new Alert()
                    {
                        Title = sipRequest.URI.Parameters.Get("topic") 
                            ?? "Direct Message",
                        Subtitle = sipRequest.Header.From.FromURI.User,
                        Body = sipRequest.Body,
                    })
                    .PlaySound("notifMessage.aif")
                    .Build();

                cancellationToken.ThrowIfCancellationRequested();
                await _apnsClient.SendAsync(notification, deviceToken);
            }
        }
    }
}
