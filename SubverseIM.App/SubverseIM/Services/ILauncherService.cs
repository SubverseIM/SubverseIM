using Avalonia;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface ILauncherService
    {
        bool NotificationsAllowed { get; }

        bool IsInForeground { get; }

        bool IsAccessibilityEnabled { get; }

        string? GetDeviceToken();

        Uri? GetLaunchedUri();

        Task<bool> ShowConfirmationDialogAsync(string title, string message);

        Task ShowAlertDialogAsync(string title, string message);

        Task<string?> ShowInputDialogAsync(string prompt, string? defaultText = null);

        Task<string?> ShowPickerDialogAsync(string prompt, string? defaultItem = null, params string[] pickerItems);

        Task ShareUrlToAppAsync(Visual? sender, string title, string content);

        Task ShareFileToAppAsync(Visual? sender, string title, string path);
    }
}
