using Foundation;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Threading;
using System.Threading.Tasks;
using UIKit;
using UserNotifications;

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
    }

    public async Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message, CancellationToken cancellationToken = default)
    {
        int notificationId = message.TopicName?.GetHashCode() ?? message.Sender.GetHashCode();

        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
        SubverseContact? contact = dbService.GetContact(message.Sender);

        UNMutableNotificationContent content = new()
        {
            Title = message.TopicName is null ? contact?.DisplayName ?? "Anonymous" :
                $"{contact?.DisplayName ?? "Anonymous"} ({message.TopicName})",
            Body = message.Content ?? string.Empty,
        };
        UNNotificationTrigger trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(15.0, false);
        UNNotificationRequest request = UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, trigger);

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
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
