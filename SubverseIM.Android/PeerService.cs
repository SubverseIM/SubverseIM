using Android.App;
using Android.Content;
using Android.OS;
using SubverseIM.Android.Services;
using SubverseIM.Services;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android
{
    [Service()]
    public class PeerService : Service, IPeerService
    {
        public IPEndPoint? LocalEndPoint => throw new System.NotImplementedException();

        public IPEndPoint? RemoteEndPoint => throw new System.NotImplementedException();

        public IDictionary<SubversePeerId, IPEndPoint?> CachedPeers => throw new System.NotImplementedException();

        public Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task ListenAsync(CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override IBinder? OnBind(Intent? intent) => 
            new ServiceBinder<IPeerService>(this);

        public Task<SubverseMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task SendMessageAsync(SubverseMessage message, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
