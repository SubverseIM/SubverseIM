using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using SubverseIM.Android.Services;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android
{
    [Service()]
    public class WrappedPeerService : Service, INativeService
    {
        private const string MSG_CHANNEL_ID = "com.ChosenFewSoftware.SubverseIM.UserMessage";

        private readonly IPeerService peerService;

        public WrappedPeerService()
        {
            peerService = new PeerService(this);
        }

        public override IBinder? OnBind(Intent? intent)
        {
            CreateNotificationChannel();
            return new ServiceBinder<IPeerService>(peerService);
        }

        private void CreateNotificationChannel()
        {
            // Create the NotificationChannel, but only on API 26+ because
            // the NotificationChannel class is not in the Support Library.
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                NotificationChannel channel = new NotificationChannel(
                    MSG_CHANNEL_ID, new Java.Lang.String("User Messages"),
                    NotificationImportance.Default);
                channel.Description = "Inbound messages from your contacts";
                // Register the channel with the system; you can't change the importance
                // or other notification behaviors after this.
                NotificationManager? manager = NotificationManager.FromContext(this);
                manager?.CreateNotificationChannel(channel);
            }
        }

        public Task SendPushNotificationAsync(int id, string title, string content, CancellationToken cancellationToken = default)
        {
            Notification notif = new NotificationCompat.Builder(this, MSG_CHANNEL_ID)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetContentTitle(title)
                .SetContentText(content)
                .SetPriority(NotificationCompat.PriorityDefault)
                .Build();

            NotificationManager? manager = NotificationManager.FromContext(this);
            manager?.Notify(id, notif);

            return Task.CompletedTask;
        }
    }
}
