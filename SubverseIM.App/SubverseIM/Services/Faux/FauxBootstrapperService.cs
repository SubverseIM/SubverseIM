using Avalonia;
using MonoTorrent;
using PgpCore;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Tests
{
    public class FauxBootstrapperService : IBootstrapperService, IInjectable, IDisposable
    {
        private readonly INativeService nativeService;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        private readonly TaskCompletionSource<IServiceManager> serviceManagerTcs;

        public FauxBootstrapperService(INativeService nativeService)
        {
            this.nativeService = nativeService;

            thisPeerTcs = new();
            serviceManagerTcs = new();
        }

        public Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromCanceled(cancellationToken);
        }

        public Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken = default)
        {
            thisPeerTcs.TrySetResult(new(RandomNumberGenerator.GetBytes(20)));
            return thisPeerTcs.Task;
        }

        public Task<IList<PeerInfo>> GetPeerInfoAsync(SubversePeerId toPeer, CancellationToken cancellationToken = default)
        {
            throw new PlatformNotSupportedException();
        }

        public Task<EncryptionKeys> GetPeerKeysAsync(SubversePeerId otherPeer, CancellationToken cancellationToken = default)
        {
            throw new PlatformNotSupportedException();
        }

        public Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken = default)
        {
            throw new PlatformNotSupportedException();
        }

        public Task InjectAsync(IServiceManager serviceManager)
        {
            serviceManagerTcs.SetResult(serviceManager);

            serviceManager.GetOrRegister(nativeService);

            serviceManager.GetOrRegister<IConfigurationService>(new ConfigurationService(serviceManager));
            serviceManager.GetOrRegister<IMessageService>(new FauxMessageService(serviceManager));
            serviceManager.GetOrRegister<ITorrentService>(new FauxTorrentService(serviceManager));

            return Task.CompletedTask;
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
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