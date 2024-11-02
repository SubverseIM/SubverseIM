using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IServiceManager
    {
        TService GetOrRegister<TImplementation, TService>(TImplementation? instance = null) 
            where TImplementation : class, TService, new()
            where TService : class;

        TService GetOrRegister<TService>(TService instance)
            where TService : class;

        Task<TService> GetWithAwaitAsync<TService>(CancellationToken cancellationToken = default)
            where TService : class;
    }
}
