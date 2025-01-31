using Avalonia;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Services.Faux
{
    public class DefaultLauncherService : ILauncherService
    {
        public bool IsAccessibilityEnabled { get; } = false;

        public bool IsInForeground { get; } = true;

        public bool NotificationsAllowed { get; } = false;

        public Uri? GetLaunchedUri() => null;

        public Task ShareFileToAppAsync(Visual? sender, string title, string path) => Task.CompletedTask;

        public Task ShareUrlToAppAsync(Visual? sender, string title, string content) => Task.CompletedTask;

        public Task ShowAlertDialogAsync(string title, string message) => Task.CompletedTask;

        public Task<bool> ShowConfirmationDialogAsync(string title, string message) => Task.FromResult(true);

        public Task<string?> ShowInputDialogAsync(string prompt, string? defaultText = null) => Task.FromResult(defaultText);
    }
}
