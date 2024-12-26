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

namespace SubverseIM.iOS;

public class WrappedPeerService : UNUserNotificationCenterDelegate, INativeService
{
    public const string EXTRA_PARTICIPANTS_ID = "com.ChosenFewSoftware.SubverseIM.ConversationParticipants";

    public const string EXTRA_TOPIC_ID = "com.ChosenFewSoftware.SubverseIM.MessageTopic";

    private readonly IServiceManager serviceManager;

    private readonly UIApplication? appInstance;

    private readonly PeerService peerService;

    public WrappedPeerService(IServiceManager serviceManager, UIApplication? appInstance)
    {
        this.serviceManager = serviceManager;
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
        UNNotificationContent content = response.Notification.Request.Content;

        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        if (content.UserInfo.Any())
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            IEnumerable<SubverseContact> participants =
                content.UserInfo[EXTRA_PARTICIPANTS_ID]
                .ToString().Split(';')
                .Select(SubversePeerId.FromString)
                .Select(dbService.GetContact)
                .Where(x => x is not null)
                .Cast<SubverseContact>();
            string? topicName = content.UserInfo[EXTRA_TOPIC_ID] as NSString;
            frontendService.NavigateMessageView(participants, topicName);
        }
        else
        {
            frontendService.NavigateContactView();
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

    public static implicit operator PeerService(WrappedPeerService instance)
    {
        return instance.peerService;
    }
}
