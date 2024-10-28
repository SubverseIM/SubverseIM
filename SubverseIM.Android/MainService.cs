using Android.App;
using Android.Content;
using Android.OS;
using SubverseIM.Android.Services;

namespace SubverseIM.Android
{
    [Service(/* TODO: Manifest definitions */)]
    public class MainService : Service
    {
        public override IBinder? OnBind(Intent? intent)
        {
            return new PeerService();
        }
    }
}
