using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
using BackgroundTasks;
using CoreGraphics;
using Foundation;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UIKit;
using UniformTypeIdentifiers;
using UserNotifications;

namespace SubverseIM.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>, ILauncherService
{
    private const string BG_TASK_ID = "com.chosenfewsoftware.SubverseIM.AppRefresh";

    private ServiceManager? serviceManager;

    private WrappedBootstrapperService? wrappedBootstrapperService;

    private Uri? launchedUri;

    private string? reminderNotificationId;

    public bool IsInForeground { get; private set; }

    public bool NotificationsAllowed { get; private set; }

    public bool IsAccessibilityEnabled => UIAccessibility.IsVoiceOverRunning;

    public AppDelegate()
    {
        UIApplication.Notifications.ObserveDidBecomeActive(HandleAppActivated);
    }

    private bool ScheduleAppRefresh(out NSError? error)
    {
        BGAppRefreshTaskRequest request = new(BG_TASK_ID)
        {
            EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(15 * 60),
        };
        return BGTaskScheduler.Shared.Submit(request, out error);
    }

    private void HandleAppActivated(object? sender, EventArgs ev)
    {
        HandleAppActivated(this, new ActivatedEventArgs(ActivationKind.Background));
    }

    private async void HandleAppActivated(object? sender, ActivatedEventArgs ev)
    {
        if (IsInForeground = ev is not AppRefreshActivatedEventArgs)
        {
            await Task.Yield();
            Window!.MakeKeyAndVisible();
        }

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
        if ((launchedUri = (ev as ProtocolActivatedEventArgs)?.Uri) is not null)
        {
            frontendService?.NavigateLaunchedUri();
        }

        if (IsInForeground && frontendService is not null)
        {
            await frontendService.RunOnceBackgroundAsync();
        }
        else if (ev is AppRefreshActivatedEventArgs ev_ && frontendService is not null)
        {
            try
            {
                using CancellationTokenSource cts = new();
                ev_.RefreshTask.ExpirationHandler += cts.Cancel;
                await frontendService.RunOnceAsync(cts.Token);
            }
            catch (Exception ex)
            {
                bool success = ex is OperationCanceledException;
                ev_.RefreshTask.SetTaskCompleted(success);
                if (!success) { throw; }
            }
        }
    }

    private async void HandleAppDeactivated(object? sender, ActivatedEventArgs ev)
    {
        IsInForeground = false;
        ScheduleAppRefresh(out NSError? _);

        UNMutableNotificationContent content = new()
        {
            Title = "Still There?",
            Body = "SubverseIM has stopped monitoring the network for new messages. We'll try our best to keep you posted!",
        };

        UNNotificationTrigger trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(30.0, false);
        UNNotificationRequest request = UNNotificationRequest.FromIdentifier(
            reminderNotificationId = Guid.NewGuid().ToString(), content, trigger
            );

        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
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

        serviceManager.GetOrRegister<IBillingService>(new BillingService());

        string appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
            );
        Directory.CreateDirectory(appDataPath);
        string dbFilePath = Path.Combine(appDataPath, "SubverseIM.db");
        serviceManager.GetOrRegister<IDbService>(
            new DbService($"Filename={dbFilePath};Password=#FreeTheInternet")
            );

        wrappedBootstrapperService = new(serviceManager, application);
        serviceManager.GetOrRegister<IBootstrapperService>(
            (BootstrapperService)wrappedBootstrapperService
            );
        UNUserNotificationCenter.Current.Delegate = wrappedBootstrapperService;

        BGTaskScheduler.Shared.Register(BG_TASK_ID, null, task =>
        {
            HandleAppActivated(this, new AppRefreshActivatedEventArgs((BGAppRefreshTask)task));
        });

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
            inputView = x;
            inputView.Text = defaultText;
            inputView.BecomeFirstResponder();
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

    public async Task<string?> ShowPickerDialogAsync(string prompt, string? defaultItem = null, params string[] pickerItems)
    {
        TaskCompletionSource<string?> tcs = new();

        UIAlertController alertController = UIAlertController
            .Create(prompt, null, UIAlertControllerStyle.Alert);

        UITextField? textField = null;
        alertController.AddTextField(x =>
        {
            textField = x;
            textField.Text = defaultItem;
            textField.InputView = new PickerDialogHelper(textField, pickerItems);
            textField.BecomeFirstResponder();
        });

        UIAlertAction positiveAction = UIAlertAction
            .Create("Submit", UIAlertActionStyle.Default, x => tcs.SetResult(textField?.Text));
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

    private Task ShowShareSheetAsync(Visual? sender, UIActivityItemSource activityItemSource)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(sender);

        UIActivityViewController activityViewController = new([activityItemSource], null);
        UIPopoverPresentationController? popoverPresentationController =
            activityViewController.PopoverPresentationController;

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

    public Task ShareUrlToAppAsync(Visual? sender, string title, string contentUrl)
    {
        return ShowShareSheetAsync(sender, new CustomActivityItemSource(
            title, new NSUrl(contentUrl), UTTypes.Url.Identifier));
    }

    public Task ShareFileToAppAsync(Visual? sender, string title, string contentPath)
    {
        return ShowShareSheetAsync(sender, new CustomActivityItemSource(
            title, NSUrl.CreateFileUrl(contentPath), UTTypes.Data.Identifier));
    }
}
