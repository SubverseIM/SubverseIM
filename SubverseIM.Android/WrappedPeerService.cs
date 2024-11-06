using Android.App;
using Android.Content;
using Android.OS;
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
        private readonly IPeerService peerService;

        public WrappedPeerService() 
        {
            peerService = new PeerService(this);
        }

        public override IBinder? OnBind(Intent? intent) => 
            new ServiceBinder<IPeerService>(peerService);

        Task INativeService.SendPushNotificationAsync(string title, string content, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        Task INativeService.ShareStringToAppAsync(string title, string content, CancellationToken cancellationToken)
        {
            Intent sendIntent = new ();
            sendIntent.SetAction(Intent.ActionSend);
            sendIntent.PutExtra(Intent.ExtraText, content);
            sendIntent.SetType("text/plain");

            Intent? shareIntent = Intent.CreateChooser(sendIntent, title);
            StartActivity(shareIntent);

            return Task.CompletedTask;
        }
    }
}
