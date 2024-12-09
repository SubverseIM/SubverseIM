using SubverseIM.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface INativeService
    {
        void ClearNotification(SubverseMessage message);

        Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message, CancellationToken cancellationToken = default);

        Task RunInBackgroundAsync(Func<CancellationToken, Task> taskFactory);
    }
}
