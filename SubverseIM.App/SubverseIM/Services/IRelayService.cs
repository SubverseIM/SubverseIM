using SIPSorcery.SIP;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IRelayService
    {
        Task<SIPMessageBase?> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task<bool> SendMessageAsync(CancellationToken cancellationToken = default);

        Task QueueMessageAsync(SIPMessageBase sipMessage);
    }
}
