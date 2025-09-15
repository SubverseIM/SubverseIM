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
        private readonly ConcurrentBag<TaskCompletionSource<SubverseMessage>> messageBag;

        private readonly IServiceManager serviceManager;

        public IPEndPoint LocalEndPoint { get; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        public MessageService(IServiceManager serviceManager)
        {
            messageBag = new();
            this.serviceManager = serviceManager;

            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, IBootstrapperService.DEFAULT_PORT_NUMBER);
            CachedPeers = new Dictionary<SubversePeerId, SubversePeer>();
        }

        public Task ListenRelayAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (messageBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
            {
                return tcs.Task.WaitAsync(cancellationToken);
            }
            else
            {
                messageBag.Add(tcs = new());
                return tcs.Task.WaitAsync(cancellationToken);
            }
        }

        public async Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
        {
            IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
            if (!messageBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
            {
                messageBag.Add(tcs = new());
            }
            await Task.Delay(1000);
            tcs.SetResult(new SubverseMessage
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