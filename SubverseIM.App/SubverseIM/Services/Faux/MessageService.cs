using SIPSorcery.SIP;
using SubverseIM.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    internal class MessageService : IMessageService, IDisposable
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

        public Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            if (messageBag.TryTake(out TaskCompletionSource<SubverseMessage>? tcs))
            {
                return tcs.Task.WaitAsync(cancellationToken);
            }
            else
            {
                messageBag.Add(tcs = new());
                return tcs.Task;
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
                CallId = CallProperties.CreateNewCallId(),

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

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~FauxMessageService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}