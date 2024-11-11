using SubverseIM.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface INativeService
    {
        void ClearNotificationForPeer(SubversePeerId otherPeer);

        Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message, CancellationToken cancellationToken = default);
    }
}
