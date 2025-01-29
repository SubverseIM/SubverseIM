using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Tests;

namespace SubverseIM.Headless
{
    public class WrappedFauxBootstrapperService : INativeService
    {
        private readonly FauxBootstrapperService instance;

        public WrappedFauxBootstrapperService() 
        {
            instance = new FauxBootstrapperService(this);
        }

        public async Task RunInBackgroundAsync(Func<CancellationToken, Task> taskFactory, CancellationToken cancellationToken = default)
        {
            try
            {
                await taskFactory(cancellationToken);
            }
            catch (OperationCanceledException) { }
        }

        public Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message) => Task.CompletedTask;

        public Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseTorrent torrent) => Task.CompletedTask;

        public void ClearNotification(SubverseMessage message) { }

        public static implicit operator FauxBootstrapperService(WrappedFauxBootstrapperService wrapper) 
        {
            return wrapper.instance;
        }
    }
}
