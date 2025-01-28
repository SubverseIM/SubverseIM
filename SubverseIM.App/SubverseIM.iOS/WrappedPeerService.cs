using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UIKit;
using UserNotifications;
using System.Linq;
using Foundation;
using MonoTorrent;
using System.Collections.Immutable;

namespace SubverseIM.iOS;

public class WrappedPeerService : UNUserNotificationCenterDelegate, INativeService
{
    public const string EXTRA_PARTICIPANTS_ID = "com.ChosenFewSoftware.SubverseIM.ConversationParticipants";

    public const string EXTRA_TOPIC_ID = "com.ChosenFewSoftware.SubverseIM.MessageTopic";

    public const string EXTRA_URI_ID = "com.ChosenFewSoftware.SubverseIM.LaunchUri";

    private readonly IServiceManager serviceManager;

    private readonly UIApplication? appInstance;

    private readonly BootstrapperService peerService;

    public WrappedPeerService(IServiceManager serviceManager, UIApplication? appInstance)
    {
        this.serviceManager = serviceManager;
        this.appInstance = appInstance;
        peerService = new BootstrapperService(this);
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
            Sound = UNNotificationSound.Default
        };

        if (message.TopicName != "#system")
        {
            var extraData = new NSDictionary<NSString, NSString?>(
                [(NSString)EXTRA_PARTICIPANTS_ID, (NSString)EXTRA_TOPIC_ID],
                [
                    (NSString)string.Join(';', ((IEnumerable<SubversePeerId>)
                [message.Sender, .. message.Recipients])
                .Select(x => x.ToString())),
                (NSString?)message.TopicName
                ]);
            content.UserInfo = extraData;
        }

        UNNotificationTrigger trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(5.0, false);
        UNNotificationRequest request = UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, trigger);

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
    }

    public async Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseTorrent torrent)
    {
        UNMutableNotificationContent content = new()
        {
            Title = MagnetLink.Parse(torrent.MagnetUri).Name ?? "Untitled",
            Body = "File was downloaded successfully.",
            Sound = UNNotificationSound.Default,
        };
        var extraData = new NSDictionary<NSString, NSString?>(
                [(NSString)EXTRA_URI_ID],
                [(NSString)torrent.MagnetUri]
                );
        content.UserInfo = extraData;

        UNNotificationTrigger trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(5.0, false);
        UNNotificationRequest request = UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, trigger);

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
    }

    public async Task RunInBackgroundAsync(Func<CancellationToken, Task> taskFactory, CancellationToken cancellationToken)
    {
        if (appInstance is null)
        {
            try
            {
                await taskFactory(cancellationToken);
            }
            catch (OperationCanceledException) { }
        }
        else
        {
            using CancellationTokenSource cts = new();
            nint handle = appInstance.BeginBackgroundTask(cts.Cancel);
            try
            {
                await taskFactory(cts.Token);
            }
            catch (OperationCanceledException) { }
            appInstance.EndBackgroundTask(handle);
        }
    }

    public override async void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();

        UNNotificationContent content = response.Notification.Request.Content;
        HashSet<string> extraDataKeys = (from kv in content.UserInfo
                                         where kv.Key is NSString
                                         select (string)(NSString)kv.Key)
                                         .ToHashSet();

        if (extraDataKeys.Contains(EXTRA_URI_ID))
        {
            string uriString = (string)(NSString)content.UserInfo[EXTRA_URI_ID];
            frontendService.NavigateLaunchedUri(new (uriString));
        }
        else if (extraDataKeys.Contains(EXTRA_PARTICIPANTS_ID))
        {
            IEnumerable<SubverseContact>? participants = 
                ((string)(NSString)content.UserInfo
                [EXTRA_PARTICIPANTS_ID]).Split(';')
                .Select(SubversePeerId.FromString)
                .Select(dbService.GetContact)
                .Where(x => x is not null)
                .Cast<SubverseContact>();
            string? topicName = content.UserInfo[EXTRA_TOPIC_ID] as NSString;
            frontendService.NavigateMessageView(participants, topicName);
        }

        completionHandler();
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

    public static implicit operator BootstrapperService(WrappedPeerService instance)
    {
        return instance.peerService;
    }
}
