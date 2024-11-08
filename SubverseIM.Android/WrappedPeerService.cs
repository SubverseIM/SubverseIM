using Android.App;
using Android.Content;
using Android.OS;
using SubverseIM.Android.Services;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;

namespace SubverseIM.Android
{
    [Service()]
    public class WrappedPeerService : Service
    {
        private readonly IPeerService peerService;

        public WrappedPeerService() 
        {
            peerService = new PeerService();
        }

        public override IBinder? OnBind(Intent? intent)
        {
            return new ServiceBinder<IPeerService>(peerService);
        }
    }
}
