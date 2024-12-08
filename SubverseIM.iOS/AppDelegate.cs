using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
using BackgroundTasks;
using Foundation;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UIKit;

namespace SubverseIM.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>, ILauncherService
{
    private const string BGTASK_BOOTSTRAP_ID = "com.chosenfewsoftware.SubverseIM.bootstrap";

    private readonly ServiceManager serviceManager;

    private WrappedPeerService? wrappedPeerService;

    private Uri? launchedUri;

    public bool IsInForeground { get; private set; }

    public bool NotificationsAllowed { get; private set; }

    public AppDelegate()
    {
        serviceManager = new();
    }

    [Export("application:didFinishLaunchingWithOptions:")]
    new public bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        base.FinishedLaunching(application, launchOptions);
        BGTaskScheduler.Shared.Register(BGTASK_BOOTSTRAP_ID, null, HandleAppRefresh);

        ((IAvaloniaAppDelegate)this).Deactivated += (s, ev) =>
        {
            IsInForeground = false;

            ScheduleAppRefresh();
        };

        ((IAvaloniaAppDelegate)this).Activated += async (s, ev) =>
        {
            IsInForeground = true;
            launchedUri = (ev as ProtocolActivatedEventArgs)?.Uri;

            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            await frontendService.RunOnceAsync();
        };

        launchedUri = launchOptions?[UIApplication.LaunchOptionsUrlKey] as NSUrl;

        serviceManager.GetOrRegister<ILauncherService>(this);

        string appDataPath = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ApplicationData
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

        return true;
    }

    private void ScheduleAppRefresh()
    {
        BGAppRefreshTaskRequest request = new BGAppRefreshTaskRequest(BGTASK_BOOTSTRAP_ID);
        request.EarliestBeginDate = NSDate.Now.AddSeconds(60.0);
        BGTaskScheduler.Shared.Submit(request, out NSError? _);
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

    public async void HandleAppRefresh(BGTask task) 
    {
        ScheduleAppRefresh();

        using CancellationTokenSource cts = new();

        BGAppRefreshTask refreshTask = (BGAppRefreshTask)task;
        refreshTask.ExpirationHandler += cts.Cancel;

        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        await frontendService.RunOnceAsync(cts.Token);
    }

    public Uri? GetLaunchedUri()
    {
        return launchedUri;
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        TaskCompletionSource<bool> tcs = new();

        UIAlertController alertController = UIAlertController.Create(
            title, message, UIAlertControllerStyle.Alert);

        UIAlertAction positiveAction = UIAlertAction
            .Create("Yes", UIAlertActionStyle.Destructive, x => tcs.SetResult(true));
        alertController.AddAction(positiveAction);

        UIAlertAction negativeAction = UIAlertAction
            .Create("No", UIAlertActionStyle.Default, x => tcs.SetResult(false));
        alertController.AddAction(positiveAction);

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

    public Task ShareStringToAppAsync(string title, string content, CancellationToken cancellationToken = default)
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
                animated: true)
            .WaitAsync(cancellationToken) ?? 
            Task.FromCanceled(cancellationToken);
    }
}
