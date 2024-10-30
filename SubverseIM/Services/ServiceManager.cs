using System;
using System.Collections.Generic;

namespace SubverseIM.Services
{
    public class ServiceManager : IServiceManager
    {
        private readonly Dictionary<Type, object> serviceMap;

        public ServiceManager()
        {
            serviceMap = new();
        }

        public TService GetOrRegister<TImplementation, TService>(TImplementation? instance)
            where TImplementation : class, TService, new()
            where TService : class
        {
            lock (serviceMap)
            {
                bool keyExists = serviceMap.TryGetValue(typeof(TService), out object? currInstance);
                TService newInstance = currInstance as TService ?? instance ?? new TImplementation();

                if (!keyExists) 
                {
                    serviceMap.Add(typeof(TService), newInstance);
                }

                return newInstance;
            }
        }

        public TService? GetOrRegister<TService>(TService? instance) 
            where TService : class
        {
            lock (serviceMap)
            {
                bool keyExists = serviceMap.TryGetValue(typeof(TService), out object? currInstance);
                TService? newInstance = currInstance as TService ?? instance;

                if (!keyExists && newInstance is not null)
                {
                    serviceMap.Add(typeof(TService), newInstance);
                }

                return newInstance;
            }
        }
    }
}
