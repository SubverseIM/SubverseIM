using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Faux;

namespace SubverseIM.Headless.Services;

public class WrappedBootstrapperService : INativeService
{
    private readonly BootstrapperService instance;

    public WrappedBootstrapperService()
    {
        instance = new BootstrapperService(this);
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

    public HttpMessageHandler GetNativeHttpHandlerInstance()
    {
        return new SocketsHttpHandler();
    }

    public static implicit operator BootstrapperService(WrappedBootstrapperService wrapper)
    {
        return wrapper.instance;
    }
}
