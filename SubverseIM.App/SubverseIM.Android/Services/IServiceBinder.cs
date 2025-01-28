using System;

namespace SubverseIM.Android.Services
{
    internal interface IServiceBinder
    {
        Type ServiceType { get; }

        object ServiceInstance { get; }
    }

    internal interface IServiceBinder<TService> : IServiceBinder
        where TService : class 
    {
        new TService ServiceInstance { get; }
    }
}
