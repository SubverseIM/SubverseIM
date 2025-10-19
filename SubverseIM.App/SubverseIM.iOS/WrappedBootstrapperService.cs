using Foundation;
using MonoTorrent;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UIKit;
using UserNotifications;

namespace SubverseIM.iOS;

public class WrappedBootstrapperService : UNUserNotificationCenterDelegate, INativeService
{
    public const string EXTRA_PARTICIPANTS_ID = "PARTICIPANTS_LIST";

    public const string EXTRA_TOPIC_ID = "MESSAGE_TOPIC";

    public const string EXTRA_URI_ID = "LAUNCH_URI";

    private readonly IServiceManager serviceManager;

    private readonly UIApplication? appInstance;

    private readonly BootstrapperService bootstrapperService;

    public WrappedBootstrapperService(IServiceManager serviceManager, UIApplication? appInstance)
    {
        this.serviceManager = serviceManager;
        this.appInstance = appInstance;
        bootstrapperService = new BootstrapperService(this);
    }

    public void ClearNotification(SubverseMessage message) { }

    public async Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        SubverseContact? contact = await dbService.GetContactAsync(message.Sender);

        UNMutableNotificationContent content = new()
        {
            Title = message.TopicName ?? "Direct Message",
            Subtitle = contact?.DisplayName ?? "Anonymous",
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

            content.Sound = UNNotificationSound.GetSound("notifMessage.aif");
        }
        else
        {
            content.Sound = UNNotificationSound.GetSound("notifSystem.aif");
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
            Sound = UNNotificationSound.GetSound("notifFile.aif")
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
            await frontendService.NavigateLaunchedUriAsync(new(uriString));
        }
        else if (extraDataKeys.Contains(EXTRA_PARTICIPANTS_ID))
        {
            IEnumerable<SubverseContact>? participants = (await Task.WhenAll(
                ((string)(NSString)content.UserInfo
                [EXTRA_PARTICIPANTS_ID]).Split(';')
                .Select(SubversePeerId.FromString)
                .Select(x => dbService.GetContactAsync(x))))
                .Where(x => x is not null)
                .Cast<SubverseContact>();
            string? topicName = content.UserInfo[EXTRA_TOPIC_ID] as NSString;
            await frontendService.NavigateMessageViewAsync(participants, topicName);
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

    public static implicit operator BootstrapperService(WrappedBootstrapperService instance)
    {
        return instance.bootstrapperService;
    }
}
