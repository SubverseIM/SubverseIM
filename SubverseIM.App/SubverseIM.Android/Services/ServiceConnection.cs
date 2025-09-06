using Android.Content;
using Android.OS;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android.Services
{
    internal class ServiceConnection<TService> : Java.Lang.Object, IServiceConnection
        where TService : class
    {
        private readonly TaskCompletionSource<TService> instanceTcs;

        private bool isConnected;

        public bool IsConnected => isConnected;

        public ServiceConnection()
        {
            instanceTcs = new();
        }

        public Task<TService> ConnectAsync(CancellationToken cancellationToken = default) 
        {
            if (isConnected)
            {
                throw new InvalidOperationException("An attempt was made to redundantly establish a connection to the service.");
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                return instanceTcs.Task.WaitAsync(cancellationToken);
            }
        }

        void IServiceConnection.OnServiceConnected(ComponentName? name, IBinder? service)
        {
            isConnected = isConnected || 
                service is IServiceBinder<TService> binder && 
                instanceTcs.TrySetResult(binder.ServiceInstance);
        }

        void IServiceConnection.OnServiceDisconnected(ComponentName? name) 
        { 
            isConnected = false; 
        }
    }
}
