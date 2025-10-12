using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class DefaultEncryptionService : IEncryptionService
    {
        public Task<string?> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(IDbService.SECRET_PASSWORD);
        }
    }
}
