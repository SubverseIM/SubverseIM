using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
using BackgroundTasks;
using Foundation;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using SubverseIM.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
    private const string BGTASK_BOOTSTRAP_ID = "com.chosenfewsoftware.SubverseIM.bootstrap";

    private ServiceManager? serviceManager;

    private WrappedPeerService? wrappedPeerService;

    private Uri? launchedUri;

    public bool IsInForeground { get; private set; }

    public bool NotificationsAllowed { get; private set; }

    public bool IsAccessibilityEnabled => false;

    private void ScheduleAppRefresh()
    {
        BGAppRefreshTaskRequest request = new BGAppRefreshTaskRequest(BGTASK_BOOTSTRAP_ID);
        request.EarliestBeginDate = NSDate.Now.AddSeconds(60.0);
        BGTaskScheduler.Shared.Submit(request, out NSError? _);
    }

    private void HandleAppDeactivated(object? sender, ActivatedEventArgs e)
    {
        IsInForeground = false;
        ScheduleAppRefresh();
    }

    private async void HandleAppActivated(object? sender, ActivatedEventArgs e)
    {
        IsInForeground = true;

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

        BGTaskScheduler.Shared.Register(BGTASK_BOOTSTRAP_ID, null, HandleAppRefresh);

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

        wrappedPeerService = new(application);
        serviceManager.GetOrRegister<IPeerService>(
            (PeerService)wrappedPeerService
            );
        UNUserNotificationCenter.Current.Delegate = wrappedPeerService;

        HandleAppActivated(this, new(ActivationKind.Background));

        return true;
    }

    public async void HandleAppRefresh(BGTask task)
    {
        ScheduleAppRefresh();

        serviceManager?.Dispose();
        serviceManager = new();

        string appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
            );
        Directory.CreateDirectory(appDataPath);
        string dbFilePath = Path.Combine(appDataPath, "SubverseIM.db");
        serviceManager.GetOrRegister<IDbService>(
            new DbService($"Filename={dbFilePath};Password=#FreeTheInternet")
        );

        wrappedPeerService = new(null);
        serviceManager.GetOrRegister<IPeerService>(
            (PeerService)wrappedPeerService
            );
        UNUserNotificationCenter.Current.Delegate = wrappedPeerService;

        using CancellationTokenSource cts = new();

        BGAppRefreshTask refreshTask = (BGAppRefreshTask)task;
        refreshTask.ExpirationHandler += cts.Cancel;

        try
        {
            IFrontendService frontendService = new MainViewModel(serviceManager);
            serviceManager.GetOrRegister(frontendService);

            await frontendService.RunOnceAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
        
        refreshTask.SetTaskCompleted(true);
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
        alertController.AddTextField(x => inputView = x);

        UIAlertAction defaultAction = UIAlertAction
            .Create("Submit", UIAlertActionStyle.Default, x => tcs.SetResult(inputView?.Text));
        alertController.AddAction(defaultAction);

        await (Window?.RootViewController
            ?.PresentViewControllerAsync(
                viewControllerToPresent: alertController,
                animated: true) ?? Task.CompletedTask);

        return await tcs.Task;
    }

    public Task ShareStringToAppAsync(string title, string content)
    {
        NSItemProvider itemProvider = new(
            item: (NSString)content,
            typeIdentifier: "public.utf8-plain-text"
            );
        UIActivityItemsConfiguration configuration = new([itemProvider]);
        UIActivityViewController activityViewController = new(configuration)
        {
            Title = title,
        };

        return Window?.RootViewController
            ?.PresentViewControllerAsync(
                viewControllerToPresent:
                activityViewController,
                animated: true) ?? Task.CompletedTask;
    }
}
