using Android.App;
using Android.Content;
using Android.OS;
using SubverseIM.Android.Services;
using SubverseIM.Services;

namespace SubverseIM.Android
{
    [Service()]
    public class PeerService : Service, IPeerService
    {
        public override IBinder? OnBind(Intent? intent) => 
            new ServiceBinder<IPeerService>(this);
    }
}
