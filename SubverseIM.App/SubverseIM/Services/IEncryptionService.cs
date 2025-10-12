using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IEncryptionService
    {
        Task<string?> GetEncryptionKeyAsync(CancellationToken cancellationToken = default);
    }
}
