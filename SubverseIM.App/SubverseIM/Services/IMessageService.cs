using SubverseIM.Core;
using SubverseIM.Models;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IMessageService
    {
        public IPEndPoint LocalEndPoint { get; }

        public IDictionary<SubversePeerId, SubversePeer> CachedPeers { get; }
        
        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default);
    }
}
