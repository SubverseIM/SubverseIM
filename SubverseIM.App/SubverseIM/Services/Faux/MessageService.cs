using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class MessageService : IMessageService
    {
        private readonly ConcurrentQueue<SubverseMessage> messageQueue;

        private readonly IServiceManager serviceManager;

        public IPEndPoint LocalEndPoint { get; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public MessageService(IServiceManager serviceManager)
        {
            messageQueue = new();
            this.serviceManager = serviceManager;

            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, IBootstrapperService.DEFAULT_PORT_NUMBER);
            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
        }

        public Task ProcessRelayAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResendAllUndeliveredMessagesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SubverseMessage?> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            messageQueue.TryDequeue(out SubverseMessage? message);
            return Task.FromResult(message);
        }

        public async Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
        {
            IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
            await Task.Delay(1000);
            messageQueue.Enqueue(new SubverseMessage
            {
                MessageId = new(CallProperties.CreateNewCallId(), await bootstrapperService.GetPeerIdAsync()),

                Sender = message.Recipients[0],
                SenderName = message.RecipientNames[0],

                Recipients = [await bootstrapperService.GetPeerIdAsync()],
                RecipientNames = ["Anonymous"],

                DateSignedOn = DateTime.UtcNow,

                Content = message.Content,
                TopicName = message.TopicName,

                WasDecrypted = true,
                WasDelivered = true
            });
        }
    }
}