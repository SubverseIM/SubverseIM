using Fitomad.Apns;
using Fitomad.Apns.Entities.Notification;
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
            return _cache.SetStringAsync($"TKN-{peerId}", deviceToken, 
                new DistributedCacheEntryOptions { AbsoluteExpiration = null }, 
                cancellationToken);
        }

        public async Task SendPushNotificationAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken)
        {
            SubversePeerId toPeer;
            string? deviceToken;

            if (_apnsClient is null || sipMessage is not SIPRequest sipRequest)
            {
                throw new PushServiceException("Instance cannot fulfill this request.");
            }
            else
            {
                toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
                deviceToken = await _cache.GetStringAsync($"TKN-{toPeer}", cancellationToken);
            }

            if (!string.IsNullOrEmpty(deviceToken))
            {
                SubverseMessage message = new SubverseMessage
                { CallId = sipRequest.Header.CallId, OtherPeer = toPeer };
                lock (_context)
                {
                    if (_context.Messages.Any(x => x.CallId == message.CallId && x.OtherPeer == message.OtherPeer))
                    {
                        throw new PushServiceException("Will not send push notification for duplicate message.");
                    }
                    else
                    {
                        _context.Messages.Add(message);
                        _context.SaveChanges();
                    }
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
                    .EnableAppExtensionModification()
                    .Build();

                cancellationToken.ThrowIfCancellationRequested();
                await _apnsClient.SendAsync(notification, deviceToken);
            }
        }
    }
}
