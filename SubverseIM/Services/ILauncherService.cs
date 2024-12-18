using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

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

        Task<string?> ShowSelectionDialogAsync(string prompt, IEnumerable<string> options);

        Task ShareStringToAppAsync(string title, string content);
    }
}
