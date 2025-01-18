using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using MonoTorrent;
using SubverseIM.Android.Services;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android
{
    [Service(ForegroundServiceType = ForegroundService.TypeSpecialUse)]
    public class WrappedPeerService : Service, INativeService
    {
        private const string MSG_CHANNEL_ID = "com.ChosenFewSoftware.SubverseIM.UserMessage";

        private const string TRN_CHANNEL_ID = "com.ChosenFewSoftware.SubverseIM.TorrentService";

        private const string SRV_CHANNEL_ID = "com.ChosenFewSoftware.SubverseIM.ForegroundService";

        public const string EXTRA_PARTICIPANTS_ID = "com.ChosenFewSoftware.SubverseIM.ConversationParticipants";

        public const string EXTRA_TOPIC_ID = "com.ChosenFewSoftware.SubverseIM.MessageTopic";

        private readonly IPeerService peerService;

        private readonly Dictionary<int, NotificationCompat.MessagingStyle> notificationMap;

        public WrappedPeerService()
        {
            peerService = new PeerService(this);
            notificationMap = new();
        }

        public override IBinder? OnBind(Intent? intent)
        {
            return new ServiceBinder<IPeerService>(peerService);
        }

        public override bool OnUnbind(Intent? intent)
        {
            base.OnUnbind(intent);
            StopForeground(StopForegroundFlags.Remove);
            return false;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            base.OnStartCommand(intent, flags, startId);

            CreateNotificationChannels();

            Intent notifyIntent = new Intent(this, typeof(MainActivity));
            notifyIntent.SetAction(Intent.ActionMain);
            notifyIntent.AddCategory(Intent.CategoryLauncher);
            notifyIntent.AddFlags(ActivityFlags.NewTask);

            PendingIntent? pendingIntent = PendingIntent.GetActivity(
                this, 0, notifyIntent, PendingIntentFlags.UpdateCurrent |
                PendingIntentFlags.Immutable);

            Notification notif = new NotificationCompat.Builder(this, SRV_CHANNEL_ID)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetContentTitle("SubverseIM Peer Services")
                .SetContentText("Participating in ongoing network activities...")
                .SetOngoing(true)
                .SetContentIntent(pendingIntent)
                .Build();

            StartForeground(1000, notif);

            return StartCommandResult.Sticky;
        }

        private void CreateNotificationChannels()
        {
            NotificationChannel messageChannel = new NotificationChannel(
                MSG_CHANNEL_ID, new Java.Lang.String("User Messages"),
                NotificationImportance.High);
            messageChannel.Description = "Inbound messages from your contacts";

            NotificationChannel serviceChannel = new NotificationChannel(
                SRV_CHANNEL_ID, new Java.Lang.String("Application Services"),
                NotificationImportance.Low);
            serviceChannel.Description = "Ongoing background tasks from SubverseIM";

            NotificationChannel torrentChannel = new NotificationChannel(
                TRN_CHANNEL_ID, new Java.Lang.String("File Services"),
                NotificationImportance.High);
            messageChannel.Description = "Status updates for active file downloads";

            // Register the channel with the system; you can't change the importance
            // or other notification behaviors after this.
            NotificationManager? manager = NotificationManager.FromContext(this);
            manager?.CreateNotificationChannel(messageChannel);
            manager?.CreateNotificationChannel(serviceChannel);
            manager?.CreateNotificationChannel(torrentChannel);
        }

        public void ClearNotification(SubverseMessage message)
        {
            lock (notificationMap)
            {
                notificationMap.Remove(
                    message.TopicName?.GetHashCode() ?? 
                    message.Sender.GetHashCode()
                    );
            }
        }

        public async Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseMessage message)
        {
            int notificationId = message.TopicName?.GetHashCode() ?? message.Sender.GetHashCode();

            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            SubverseContact? contact = dbService.GetContact(message.Sender);

            Bitmap? avatarBitmap;
            if (contact?.ImagePath is not null && dbService
                .TryGetReadStream(contact.ImagePath, out Stream? avatarStream))
            {
                avatarBitmap = await BitmapFactory.DecodeStreamAsync(avatarStream);
            }
            else
            {
                avatarBitmap = null;
            }

            NotificationCompat.MessagingStyle? messagingStyle;
            lock (notificationMap)
            {
                if (!notificationMap.TryGetValue(notificationId, out messagingStyle))
                {
                    notificationMap.Add(notificationId, messagingStyle = new(message.TopicName ?? contact?.DisplayName ?? "Anonymous"));
                }
            }

            long timestamp = ((System.DateTimeOffset)message.DateSignedOn)
                .ToUnixTimeMilliseconds();
            messagingStyle.AddMessage(new(
                message.Content, timestamp,
                message.TopicName is null ? contact?.DisplayName ?? "Anonymous" :
                    $"{contact?.DisplayName ?? "Anonymous"} ({message.TopicName})"
                ));

            Uri? soundUri = Uri.Parse("android.resource://" + PackageName + "/" + Resource.Raw.notif);
            
            Intent notifyIntent = new Intent(this, typeof(MainActivity));
            notifyIntent.SetAction(Intent.ActionMain);
            notifyIntent.AddCategory(Intent.CategoryLauncher);
            notifyIntent.AddFlags(ActivityFlags.NewTask);
            if (message.TopicName != "#system")
            {
                notifyIntent.PutExtra(EXTRA_TOPIC_ID, message.TopicName);
                notifyIntent.PutExtra(EXTRA_PARTICIPANTS_ID,
                    ((IEnumerable<SubversePeerId>)
                    [message.Sender, .. message.Recipients])
                    .Select(x => x.ToString())
                    .ToArray());
            }

            PendingIntent? pendingIntent = PendingIntent.GetActivity(
                this, 0, notifyIntent, PendingIntentFlags.UpdateCurrent |
                PendingIntentFlags.Immutable);

            Notification notif = new NotificationCompat.Builder(this, MSG_CHANNEL_ID)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetLargeIcon(avatarBitmap)
                .SetSound(soundUri)
                .SetStyle(messagingStyle)
                .Build();

            NotificationManager? manager = NotificationManager.FromContext(this);
            manager?.Notify(notificationId, notif);
        }

        public Task SendPushNotificationAsync(IServiceManager serviceManager, SubverseTorrent torrent)
        {
            int notificationId = torrent.MagnetUri.GetHashCode();
            Uri? soundUri = Uri.Parse("android.resource://" + PackageName + "/" + Resource.Raw.notif);

            Intent notifyIntent = new Intent(this, typeof(MainActivity));
            notifyIntent.SetAction(Intent.ActionMain);
            notifyIntent.AddCategory(Intent.CategoryLauncher);
            notifyIntent.AddFlags(ActivityFlags.NewTask);
            notifyIntent.SetData(Uri.Parse(torrent.MagnetUri));

            PendingIntent? pendingIntent = PendingIntent.GetActivity(
                this, 0, notifyIntent, PendingIntentFlags.UpdateCurrent |
                PendingIntentFlags.Immutable);

            Notification notif = new NotificationCompat.Builder(this, MSG_CHANNEL_ID)
                .SetContentTitle(MagnetLink.Parse(torrent.MagnetUri).Name)
                .SetContentText("File was downloaded successfully.")
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetSound(soundUri)
                .Build();

            NotificationManager? manager = NotificationManager.FromContext(this);
            manager?.Notify(notificationId, notif);

            return Task.CompletedTask;
        }

        public Task RunInBackgroundAsync(System.Func<CancellationToken, Task> taskFactory, CancellationToken cancellationToken)
        {
            return taskFactory(cancellationToken);
        }
    }
}
