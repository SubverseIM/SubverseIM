using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Threading;
using System.Threading.Tasks;
using UIKit;

namespace SubverseIM.iOS;

public class WrappedPeerService : INativeService
{
    private readonly UIApplication appInstance;

    private readonly PeerService peerService;

    public WrappedPeerService(UIApplication appInstance)
    {
        this.appInstance = appInstance;
        peerService = new PeerService(this);
    }

    public void ClearNotification(SubverseMessage message)
    {
        throw new NotImplementedException();
    }

    public Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task RunInBackgroundAsync(Task task)
    {
        using CancellationTokenSource cts = new();
        nint handle = appInstance.BeginBackgroundTask(cts.Cancel);
        await task.WaitAsync(cts.Token);
        appInstance.EndBackgroundTask(handle);
    }

    public static implicit operator PeerService(WrappedPeerService instance)
    {
        return instance.peerService;
    }
}
