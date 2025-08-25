using SubverseIM.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface INativeService
    {
        void ClearNotification(SubverseMessage message);

        Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message);

        Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseTorrent torrent);

        Task RunInBackgroundAsync(Func<CancellationToken, Task> taskFactory, CancellationToken cancellationToken = default);

        HttpMessageHandler GetNativeHttpHandlerInstance();
    }
}
