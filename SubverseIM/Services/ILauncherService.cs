using System;
using System.Threading.Tasks;
using System.Threading;

namespace SubverseIM.Services
{
    public interface ILauncherService
    {
        bool NotificationsAllowed { get; }

        bool IsInForeground { get; }

        Uri? GetLaunchedUri();

        Task ShareStringToAppAsync(string title, string content, CancellationToken cancellationToken = default);
    }
}
