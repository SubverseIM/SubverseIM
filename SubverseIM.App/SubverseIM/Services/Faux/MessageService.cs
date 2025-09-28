using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class MessageService : IMessageService
    {
        private readonly Channel<SubverseMessage> messageQueue;

        private readonly IServiceManager serviceManager;

        public IPEndPoint LocalEndPoint { get; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public MessageService(IServiceManager serviceManager)
        {
            messageQueue = Channel.CreateUnbounded<SubverseMessage>();
            this.serviceManager = serviceManager;

            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, IBootstrapperService.DEFAULT_PORT_NUMBER);
            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
        }

        public Task ProcessRelayAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ResendAllUndeliveredMessagesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            return await messageQueue.Reader.ReadAsync(cancellationToken);
        }

        public async Task SendMessageAsync(SubverseMessage message, int maxSendAttempts, CancellationToken cancellationToken)
        {
            IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
            await Task.Delay(1000);
            await messageQueue.Writer.WriteAsync(new SubverseMessage
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