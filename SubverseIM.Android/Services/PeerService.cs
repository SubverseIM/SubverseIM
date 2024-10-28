using Android.OS;
using SubverseIM.Services;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android.Services
{
    internal class PeerService : Binder, IPeerService
    {
        public Task<int> GetResultAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1337);
        }
    }
}
