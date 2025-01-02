using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IRunnable
    {
        Task RunOnceAsync(CancellationToken cancellationToken = default);
    }
}
