using Android.Content;
using Android.OS;
using SubverseIM.Services;
using System;
using System.Collections.Generic;

namespace SubverseIM.Android.Services
{
    internal class ServiceManager : Java.Lang.Object, IServiceConnection, IServiceManager
    {
        private readonly Dictionary<string, Type> serviceNameMap;
        private readonly Dictionary<Type, object> serviceMap;

        public ServiceManager()
        {
            serviceNameMap = new();
            serviceMap = new();
        }

        TService? IServiceManager.Get<TService>() where TService : class
        {
            if (serviceMap.TryGetValue(typeof(TService), out object? instance))
            {
                return instance as TService;
            }
            else 
            {
                return null;
            }
        }

        void IServiceConnection.OnServiceConnected(ComponentName? name, IBinder? service)
        {
            if (name is not null && service is IServiceBinder binder)
            {
                serviceNameMap.Add(name.FlattenToString(), binder.ServiceType);
                serviceMap.Add(binder.ServiceType, binder.ServiceInstance);
            }
        }

        void IServiceConnection.OnServiceDisconnected(ComponentName? name)
        {
            if (name is not null && serviceNameMap.Remove(name.FlattenToString(), out Type? type))
            {
                serviceMap.Remove(type);
            }
        }
    }
}
