using SubverseIM.Core;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public class MessageReceivedEventArgs : EventArgs
    {
        private bool shouldSendPushNotif;
        public bool ShouldSendPushNotif
        {
            get => shouldSendPushNotif;
            set => shouldSendPushNotif &= value;
        }

        public SubverseMessage Message { get; }

        public MessageReceivedEventArgs(SubverseMessage message)
        {
            shouldSendPushNotif = true;
            Message = message;
        }
    }

    public interface IMessageService
    {
        const int DEFAULT_MAX_SEND_ATTEMPTS = 10;

        event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        Task ProcessRelayAsync(CancellationToken cancellationToken = default);

        Task ResendAllUndeliveredMessagesAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, int maxSendAttempts = DEFAULT_MAX_SEND_ATTEMPTS, CancellationToken cancellationToken = default);

        Task RunAsync(CancellationToken cancellationToken = default);
    }
}
