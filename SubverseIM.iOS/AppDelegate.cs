using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
using CoreGraphics;
using Foundation;
using MonoTorrent.Client;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.IO;
using System.Threading.Tasks;
using UIKit;
using UserNotifications;

namespace SubverseIM.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>, ILauncherService
{
    private ServiceManager? serviceManager;

    private WrappedPeerService? wrappedPeerService;

    private Uri? launchedUri;

    private string? reminderNotificationId;

    public bool IsInForeground { get; private set; }

    public bool NotificationsAllowed { get; private set; }

    public bool IsAccessibilityEnabled => false;

    private async void HandleAppDeactivated(object? sender, ActivatedEventArgs e)
    {
        IsInForeground = false;

        UNMutableNotificationContent content = new()
        {
            Title = "Still There?",
            Body = "SubverseIM has stopped monitoring the network for new messages. Check back with us often!"
        };

        UNNotificationTrigger trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(30.0, false);
        UNNotificationRequest request = UNNotificationRequest.FromIdentifier(
            reminderNotificationId = Guid.NewGuid().ToString(), content, trigger
            );

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
    }

    private async void HandleAppActivated(object? sender, ActivatedEventArgs e)
    {
        IsInForeground = true;

        if (reminderNotificationId is not null)
        {
            UNUserNotificationCenter.Current.RemovePendingNotificationRequests([reminderNotificationId]);
        }

        UNNotificationSettings settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
        {
            (bool result, NSError? _) = await UNUserNotificationCenter.Current
                .RequestAuthorizationAsync(options: UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound);
            NotificationsAllowed = result;
        }
        else
        {
            NotificationsAllowed = true;
        }

        Task<IFrontendService>? resolveServiceTask = serviceManager?.GetWithAwaitAsync<IFrontendService>();
        IFrontendService? frontendService = resolveServiceTask is null ? null : await resolveServiceTask;
        if ((launchedUri = (e as ProtocolActivatedEventArgs)?.Uri) is not null)
        {
            frontendService?.NavigateLaunchedUri();
        }
        await (frontendService?.RunOnceBackgroundAsync() ?? Task.CompletedTask);
    }

    protected override AppBuilder CreateAppBuilder()
    {
        return AppBuilder.Configure(() => new App(serviceManager)).UseiOS();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI();
    }

    public Uri? GetLaunchedUri()
    {
        return launchedUri;
    }

    [Export("application:didFinishLaunchingWithOptions:")]
    new public bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        serviceManager?.Dispose();
        serviceManager = new();

        base.FinishedLaunching(application, launchOptions);

        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();

        ((IAvaloniaAppDelegate)this).Deactivated += HandleAppDeactivated;
        ((IAvaloniaAppDelegate)this).Activated += HandleAppActivated;

        launchedUri = launchOptions?[UIApplication.LaunchOptionsUrlKey] as NSUrl;
        serviceManager.GetOrRegister<ILauncherService>(this);

        string appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
            );
        Directory.CreateDirectory(appDataPath);
        string dbFilePath = Path.Combine(appDataPath, "SubverseIM.db");
        serviceManager.GetOrRegister<IDbService>(
            new DbService($"Filename={dbFilePath};Password=#FreeTheInternet")
            );

        wrappedPeerService = new(serviceManager, application);
        serviceManager.GetOrRegister<IPeerService>(
            (PeerService)wrappedPeerService
            );
        UNUserNotificationCenter.Current.Delegate = wrappedPeerService;

        HandleAppActivated(this, new(ActivationKind.Background));

        return true;
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        TaskCompletionSource<bool> tcs = new();

        UIAlertController alertController = UIAlertController.Create(
            title, message, UIAlertControllerStyle.Alert);

        UIAlertAction positiveAction = UIAlertAction
            .Create("Yes", UIAlertActionStyle.Default, x => tcs.SetResult(true));
        alertController.AddAction(positiveAction);

        UIAlertAction negativeAction = UIAlertAction
            .Create("No", UIAlertActionStyle.Cancel, x => tcs.SetResult(false));
        alertController.AddAction(negativeAction);

        await (Window?.RootViewController?.PresentViewControllerAsync(
                alertController, true) ?? Task.CompletedTask);

        return await tcs.Task;
    }

    public async Task ShowAlertDialogAsync(string title, string message)
    {
        TaskCompletionSource tcs = new();

        UIAlertController alertController = UIAlertController
            .Create(title, message, UIAlertControllerStyle.Alert);

        UIAlertAction defaultAction = UIAlertAction
            .Create("OK", UIAlertActionStyle.Default, x => tcs.SetResult());
        alertController.AddAction(defaultAction);

        await (Window?.RootViewController
            ?.PresentViewControllerAsync(
                viewControllerToPresent: alertController,
                animated: true) ?? Task.CompletedTask);

        await tcs.Task;
    }

    public async Task<string?> ShowInputDialogAsync(string prompt, string? defaultText = null)
    {
        TaskCompletionSource<string?> tcs = new();

        UIAlertController alertController = UIAlertController
            .Create(prompt, null, UIAlertControllerStyle.Alert);

        UITextField? inputView = null;
        alertController.AddTextField(x =>
        {
            x.Text = defaultText;
            inputView = x;
        });

        UIAlertAction positiveAction = UIAlertAction
            .Create("Submit", UIAlertActionStyle.Default, x => tcs.SetResult(inputView?.Text));
        alertController.AddAction(positiveAction);

        UIAlertAction negativeAction = UIAlertAction
            .Create("Cancel", UIAlertActionStyle.Cancel, x => tcs.SetResult(null));
        alertController.AddAction(negativeAction);

        await (Window?.RootViewController
            ?.PresentViewControllerAsync(
                viewControllerToPresent: alertController,
                animated: true) ?? Task.CompletedTask);

        return await tcs.Task;
    }

    public Task ShareStringToAppAsync(Visual? sender, string title, string content)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(sender);

        NSItemProvider itemProvider = new(
            item: (NSString)content,
            typeIdentifier: "public.utf8-plain-text"
            );

        UIActivityItemsConfiguration configuration = new([itemProvider]);
        UIActivityViewController activityViewController = new(configuration)
        {
            Title = title,
        };

        UIPopoverPresentationController? popoverPresentationController = activityViewController.PopoverPresentationController;
        if (topLevel is not null && sender is not null &&
            UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad &&
            popoverPresentationController is not null && Window is not null)
        {
            PixelPoint topLeft = topLevel.PointToScreen(sender.Bounds.TopLeft);
            PixelPoint bottomRight = topLevel.PointToScreen(sender.Bounds.BottomRight);

            popoverPresentationController.SourceView = Window;
            popoverPresentationController.SourceRect = new CGRect(
                topLeft.X, topLeft.Y, (bottomRight - topLeft).X, (bottomRight - topLeft).Y
                );

            activityViewController.ModalPresentationStyle = UIModalPresentationStyle.PageSheet;
        }

        return Window?.RootViewController
            ?.PresentViewControllerAsync(
                viewControllerToPresent:
                activityViewController,
                animated: true) ?? Task.CompletedTask;
    }

    public Task ShareUriToAppAsync(Visual? sender, string title, Uri uri)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(sender);

        UIActivityItemsConfiguration configuration = new([(NSUrl)uri!]);
        UIActivityViewController activityViewController = new(configuration)
        {
            Title = title,
        };

        UIPopoverPresentationController? popoverPresentationController = activityViewController.PopoverPresentationController;
        if (topLevel is not null && sender is not null &&
            UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad &&
            popoverPresentationController is not null && Window is not null)
        {
            PixelPoint topLeft = topLevel.PointToScreen(sender.Bounds.TopLeft);
            PixelPoint bottomRight = topLevel.PointToScreen(sender.Bounds.BottomRight);

            popoverPresentationController.SourceView = Window;
            popoverPresentationController.SourceRect = new CGRect(
                topLeft.X, topLeft.Y, (bottomRight - topLeft).X, (bottomRight - topLeft).Y
                );

            activityViewController.ModalPresentationStyle = UIModalPresentationStyle.PageSheet;
        }

        return Window?.RootViewController
            ?.PresentViewControllerAsync(
                viewControllerToPresent:
                activityViewController,
                animated: true) ?? Task.CompletedTask;   
    }
}
