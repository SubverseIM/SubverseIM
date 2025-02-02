using Avalonia;
using MonoTorrent;
using PgpCore;
using SubverseIM.Models;
using SubverseIM.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class BootstrapperService : IBootstrapperService, IInjectable
    {
        private readonly INativeService nativeService;

        private readonly TaskCompletionSource<SubversePeerId> thisPeerTcs;

        private readonly TaskCompletionSource<IServiceManager> serviceManagerTcs;

        public BootstrapperService(INativeService nativeService)
        {
            this.nativeService = nativeService;

            thisPeerTcs = new();
            serviceManagerTcs = new();
        }

        public Task BootstrapSelfAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromException(new PlatformNotSupportedException());
        }

        public Task<SubversePeerId> GetPeerIdAsync(CancellationToken cancellationToken = default)
        {
            thisPeerTcs.TrySetResult(new(RandomNumberGenerator.GetBytes(20)));
            return thisPeerTcs.Task;
        }

        public Task<IList<PeerInfo>> GetPeerInfoAsync(SubversePeerId toPeer, CancellationToken cancellationToken = default)
        {
            return Task.FromException<IList<PeerInfo>>(new PlatformNotSupportedException());
        }

        public Task<EncryptionKeys> GetPeerKeysAsync(SubversePeerId otherPeer, CancellationToken cancellationToken = default)
        {
            return Task.FromException<EncryptionKeys>(new PlatformNotSupportedException());
        }

        public Task SendInviteAsync(Visual? sender, CancellationToken cancellationToken = default)
        {
            return Task.FromException(new PlatformNotSupportedException());
        }

        public Task InjectAsync(IServiceManager serviceManager)
        {
            serviceManagerTcs.SetResult(serviceManager);

            serviceManager.GetOrRegister(nativeService);
            serviceManager.GetOrRegister<IConfigurationService>(new ConfigurationService(serviceManager));
            serviceManager.GetOrRegister<IMessageService>(new MessageService(serviceManager));
            serviceManager.GetOrRegister<ITorrentService>(new TorrentService(serviceManager));

            return Task.CompletedTask;
        }
    }
}