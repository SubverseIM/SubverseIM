using SubverseIM.Core;
using SubverseIM.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IMessageService
    {
        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }

        Task ProcessRelayAsync(CancellationToken cancellationToken = default);

        Task ResendAllUndeliveredMessagesAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default);
    }
}
