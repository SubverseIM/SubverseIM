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
        private readonly IDistributedCache _cache;

        private readonly IDbService _dbService;

        private readonly IApnsClient? _apnsClient;

        public PushService(IDistributedCache cache, IDbService dbService, IApnsClient apnsClient)
        {
            _cache = cache;
            _dbService = dbService;
            _apnsClient = apnsClient;
        }

        public PushService(IDistributedCache cache, IDbService dbService)
        {
            _cache = cache;
            _dbService = dbService;
        }

        public Task RegisterPeerAsync(SubversePeerId peerId, byte[] deviceToken, CancellationToken cancellationToken)
        {
            return _cache.SetAsync($"TKN-{peerId}", deviceToken,
                new DistributedCacheEntryOptions { AbsoluteExpiration = null },
                cancellationToken);
        }

        public async Task SendPushNotificationAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken)
        {
            SubversePeerId toPeer;
            byte[]? deviceToken;

            if (_apnsClient is null || sipMessage is not SIPRequest sipRequest)
            {
                throw new PushServiceException("Instance cannot fulfill this request.");
            }
            else
            {
                toPeer = SubversePeerId.FromString(sipRequest.Header.To.ToURI.User);
                deviceToken = await _cache.GetAsync($"TKN-{toPeer}", cancellationToken);
            }

            if (deviceToken is not null)
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
                await _apnsClient.SendAsync(container, Convert.ToHexStringLower(deviceToken));
            }
        }

        public Task<bool> TryStoreMessageAsync(SIPMessageBase sipMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sipMessage is not SIPRequest) return Task.FromResult(false);

            SubversePeerId toPeer = SubversePeerId.FromString(sipMessage.Header.To.ToURI.User);
            SubverseMessage message = new()
            {
                MessageId = new (sipMessage.Header.CallId, toPeer)
            };

            return Task.FromResult(_dbService.InsertMessage(message));
        }
    }
}
