using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IFrontendService
    {
        Task ViewCreateContactAsync(Uri contactUri, CancellationToken cancellationToken = default);
    }
}
