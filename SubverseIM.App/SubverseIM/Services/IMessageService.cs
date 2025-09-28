using SubverseIM.Core;
using SubverseIM.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IMessageService
    {
        public const int DEFAULT_MAX_SEND_ATTEMPTS = 10;

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        Task ProcessRelayAsync(CancellationToken cancellationToken = default);

        Task ResendAllUndeliveredMessagesAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, int maxSendAttempts = DEFAULT_MAX_SEND_ATTEMPTS, CancellationToken cancellationToken = default);
    }
}
