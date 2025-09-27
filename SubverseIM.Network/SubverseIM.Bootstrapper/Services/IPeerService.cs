using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IPeerService
    {
        Task ReceiveMessageAsync(string rawMessage, CancellationToken cancellationToken = default);
    }
}
