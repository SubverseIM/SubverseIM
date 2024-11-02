using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface INativeService
    {
        Task SendPushNotificationAsync(string title, string content, CancellationToken cancellationToken = default);
    }
}
