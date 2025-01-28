using SubverseIM.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IMessageService
    {
        Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default);
    }
}
