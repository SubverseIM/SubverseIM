using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class DummyEncryptionService : IEncryptionService
    {
        public Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IDbService.SECRET_PASSWORD);
        }
    }
}
