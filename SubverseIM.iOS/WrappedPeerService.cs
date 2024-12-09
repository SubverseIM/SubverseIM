using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Threading;
using System.Threading.Tasks;
using UIKit;
using UserNotifications;

namespace SubverseIM.iOS;

public class WrappedPeerService : UNUserNotificationCenterDelegate, INativeService
{
    private readonly UIApplication appInstance;

    private readonly PeerService peerService;

    public WrappedPeerService(UIApplication appInstance)
    {
        this.appInstance = appInstance;
        peerService = new PeerService(this);
    }

    public void ClearNotification(SubverseMessage message) { }

    public async Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        SubverseContact? contact = dbService.GetContact(message.Sender);

        UNMutableNotificationContent content = new()
        {
            Title = message.TopicName is null ? contact?.DisplayName ?? "Anonymous" :
                $"{contact?.DisplayName ?? "Anonymous"} ({message.TopicName})",
            Body = message.Content ?? string.Empty,
        };

        UNNotificationTrigger trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(5.0, false);
        UNNotificationRequest request = UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, trigger);

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
    }

    public async Task RunInBackgroundAsync(Func<CancellationToken, Task> taskFactory)
    {
        using CancellationTokenSource cts = new();
        nint handle = appInstance.BeginBackgroundTask(cts.Cancel);
        try
        {
            await taskFactory(cts.Token);
        }
        catch(OperationCanceledException) { }
        appInstance.EndBackgroundTask(handle);
    }

    public override void WillPresentNotification(
        UNUserNotificationCenter center, UNNotification notification, 
        Action<UNNotificationPresentationOptions> completionHandler
        )
    {
        completionHandler(
            UNNotificationPresentationOptions.Banner | 
            UNNotificationPresentationOptions.List | 
            UNNotificationPresentationOptions.Sound);
    }

    public static implicit operator PeerService(WrappedPeerService instance)
    {
        return instance.peerService;
    }
}
