using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IPeerService
    {
        Task<int> GetResultAsync(CancellationToken cancellationToken = default);
    }
}
