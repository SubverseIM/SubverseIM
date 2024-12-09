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

        Task<bool> ShowConfirmationDialogAsync(string title, string message);

        Task ShowAlertDialogAsync(string title, string message);

        Task ShareStringToAppAsync(string title, string content);
    }
}
