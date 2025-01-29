using SubverseIM.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IConfigurationService
    {
        Task<SubverseConfig> GetConfigAsync(CancellationToken cancellationToken = default);

        Task<bool> PersistConfigAsync(CancellationToken cancellationToken = default);
    }
}
