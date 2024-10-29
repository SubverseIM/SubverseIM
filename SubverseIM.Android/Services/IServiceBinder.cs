using System;

namespace SubverseIM.Android.Services
{
    internal interface IServiceBinder
    {
        Type ServiceType { get; }

        object ServiceInstance { get; }
    }
}
