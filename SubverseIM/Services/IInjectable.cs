using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services;

public interface IInjectable
{
    Task InjectAsync(IServiceManager serviceManager, CancellationToken cancellationToken = default);
}
