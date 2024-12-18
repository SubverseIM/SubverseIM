using System;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface ILauncherService
    {
        bool NotificationsAllowed { get; }

        bool IsInForeground { get; }

        bool IsAccessibilityEnabled { get; }

        Uri? GetLaunchedUri();

        Task<bool> ShowConfirmationDialogAsync(string title, string message);

        Task ShowAlertDialogAsync(string title, string message);

        Task<string?> ShowInputDialogAsync(string prompt, string? defaultText = null);

        Task ShareStringToAppAsync(string title, string content);
    }
}
