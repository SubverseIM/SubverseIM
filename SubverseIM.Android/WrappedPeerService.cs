using Android.App;
using Android.Content;
using Android.OS;
using SubverseIM.Android.Services;
using SubverseIM.Services;
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

        public Task SendPushNotificationAsync(string title, string content, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
