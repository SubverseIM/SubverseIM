using Android.Content;
using Android.OS;
using SubverseIM.Services;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android.Services
{
    internal class ServiceManager<TService> : Java.Lang.Object, IServiceConnection, IServiceManager<TService>
        where TService : class
    {
        private readonly TaskCompletionSource<TService?> serviceTcs;

        public ServiceManager()
        {
            serviceTcs = new();
        }

        async Task<TService?> IServiceManager<TService>.GetInstanceAsync(CancellationToken cancellationToken)
        {
            return await serviceTcs.Task.WaitAsync(cancellationToken);
        }

        void IServiceConnection.OnServiceConnected(ComponentName? name, IBinder? service)
        {
            serviceTcs.SetResult(service as TService);
        }

        void IServiceConnection.OnServiceDisconnected(ComponentName? name) { /* STUB */ }
    }
}
