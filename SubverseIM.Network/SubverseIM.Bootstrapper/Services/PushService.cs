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
                string? topicName = sipRequest.URI.Parameters.Get("topic");
                bool isSystemTopic = topicName == "#system";

                Notification notification = new NotificationBuilder()
                    .WithAlert(new Alert()
                    {
                        Title = topicName ?? "Direct Message",
                        Subtitle = "Somebody",
                        Body = "[Contents Encrypted]",
                    })
                    .PlaySound(isSystemTopic ? "notifSystem.aif" : "notifMessage.aif")
                    .EnableAppExtensionModification()
                    .Build();

                CustomNotificationContainer container = new()
                {
                    Notification = notification,

                    BodyContent = sipRequest.Body,
                    MessageTopic = isSystemTopic ? null : topicName,
                    ParticipantsList = isSystemTopic ? null : string.Join(';', [
                        sipRequest.Header.From.FromURI.User, ..
                        sipRequest.Header.Contact.Select(x => x.ContactURI.User)
                        ]),
                    SenderId = sipRequest.Header.From.FromURI.User,
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _apnsClient.SendAsync(container, deviceToken);
            }
        }

        public bool TryStoreMessage(SIPMessageBase sipMessage)
        {
            SubversePeerId toPeer = SubversePeerId.FromString(sipMessage.Header.To.ToURI.User);
            SubverseMessage message = new SubverseMessage
            { CallId = sipMessage.Header.CallId, OtherPeer = toPeer };
            lock (_context)
            {
                if (_context.Messages.Any(x => x.CallId == message.CallId && x.OtherPeer == message.OtherPeer))
                {
                    return false;
                }
                else
                {
                    _context.Messages.Add(message);
                    _context.SaveChanges();
                    return true;
                }
            }
        }
    }
}
