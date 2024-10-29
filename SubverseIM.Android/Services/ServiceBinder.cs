using Android.OS;
using System;

namespace SubverseIM.Android.Services
{
    internal class ServiceBinder<TService> : Binder, IServiceBinder
        where TService : class
    {
        private readonly TService instance;

        public Type ServiceType => typeof(TService);

        public object ServiceInstance => instance;

        public ServiceBinder(TService instance) 
        {
            this.instance = instance;
        }
    }
}
