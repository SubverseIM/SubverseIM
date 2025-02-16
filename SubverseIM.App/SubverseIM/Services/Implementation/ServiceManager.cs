using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class ServiceManager : IServiceManager
    {
        private readonly Dictionary<Type, object> serviceMap;

        private readonly Dictionary<Type, TaskCompletionSource<object>> awaitMap;

        private bool disposedValue;

        public ServiceManager()
        {
            serviceMap = new();
            awaitMap = new();
        }

        public TService? Get<TService>()
            where TService : class
        {
            lock (serviceMap) 
            {
                serviceMap.TryGetValue(typeof(TService), out object? instance);
                return instance as TService;
            }
        }

        public TService GetOrRegister<TImplementation, TService>(TImplementation? instance)
            where TImplementation : class, TService, new()
            where TService : class
        {
            TService newInstance;
            lock (serviceMap)
            {
                bool keyExists = serviceMap.TryGetValue(typeof(TService), out object? currInstance);
                newInstance = currInstance as TService ?? instance ?? new TImplementation();

                if (!keyExists)
                {
                    serviceMap.Add(typeof(TService), newInstance);
                }
            }

            lock (awaitMap)
            {
                if (awaitMap.TryGetValue(typeof(TService), out TaskCompletionSource<object>? instanceTcs))
                {
                    instanceTcs.SetResult(newInstance);
                    awaitMap.Remove(typeof(TService));
                }
            }

            _ = (newInstance as IInjectable)?.InjectAsync(this);
            return newInstance;
        }

        public TService GetOrRegister<TService>(TService instance)
            where TService : class
        {
            TService newInstance;
            lock (serviceMap)
            {
                bool keyExists = serviceMap.TryGetValue(typeof(TService), out object? currInstance);
                newInstance = currInstance as TService ?? instance;

                if (!keyExists)
                {
                    serviceMap.Add(typeof(TService), newInstance);
                }
            }

            lock (awaitMap)
            {
                if (awaitMap.TryGetValue(typeof(TService), out TaskCompletionSource<object>? instanceTcs))
                {
                    instanceTcs.SetResult(newInstance);
                    awaitMap.Remove(typeof(TService));
                }
            }

            _ = (newInstance as IInjectable)?.InjectAsync(this);
            return newInstance;
        }

        public async Task<TService> GetWithAwaitAsync<TService>(CancellationToken cancellationToken = default) 
            where TService : class
        {
            lock (serviceMap)
            {
                if (serviceMap.TryGetValue(typeof(TService), out object? currInstance) &&
                    currInstance is TService cached)
                {
                    return cached;
                }
            }

            TaskCompletionSource<object>? instanceTcs;
            lock (awaitMap)
            {
                if (!awaitMap.TryGetValue(typeof(TService), out instanceTcs))
                {
                    instanceTcs = new();
                    awaitMap.Add(typeof(TService), instanceTcs);
                }
                else
                {
                    awaitMap.Remove(typeof(TService));
                }
            }

            return (TService)await instanceTcs.Task
                .WaitAsync(cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var (_, service) in serviceMap)
                    {
                        (service as IDisposableService)?.Dispose();
                    }
                }

                serviceMap.Clear();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
