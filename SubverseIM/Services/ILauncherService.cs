using System;
using System.Threading.Tasks;
using System.Threading;

namespace SubverseIM.Services
{
    public interface ILauncherService
    {
        Uri? GetLaunchedUri();

        Task ShareStringToAppAsync(string title, string content, CancellationToken cancellationToken = default);
    }
}
