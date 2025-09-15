using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IPeerService
    {
        Task ReceiveMessageAsync(byte[] messageBytes);

        Task RegisterPeerAsync(SubversePeerId peerId, IPeerService peer);
    }
}
