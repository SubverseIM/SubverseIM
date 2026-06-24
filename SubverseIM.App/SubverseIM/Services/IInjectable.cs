using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services;

public interface IInjectable
{
    bool UseSeparateThread { get; }

    Task InjectAsync(IServiceManager serviceManager);
}
