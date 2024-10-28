using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IServiceManager<TService> where TService : class
    {
        Task<TService?> GetInstanceAsync(CancellationToken cancellationToken = default);
    }
}
