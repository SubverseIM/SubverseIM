using Android.OS;
using System;

namespace SubverseIM.Android.Services
{
    internal class ServiceBinder<TService> : Binder, IServiceBinder<TService>
        where TService : class
    {
        public TService ServiceInstance { get; }

        public ServiceBinder(TService instance) 
        {
            ServiceInstance = instance;
        }

        Type IServiceBinder.ServiceType => typeof(TService);

        object IServiceBinder.ServiceInstance => ServiceInstance;
    }
}
