using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
using BackgroundTasks;
using Foundation;
using SubverseIM.Services.Implementation;
using UIKit;

namespace SubverseIM.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    private readonly ServiceManager serviceManager;

    public AppDelegate()
    {
        serviceManager = new();
    }

    [Export("application:willFinishLaunchingWithOptions:")]
    public bool WillFinishLaunchingWithOptions(UIApplication application, NSDictionary launchOptions)
    {
        BGTaskScheduler.Shared.Register("com.chosenfewsoftware.SubverseIM.bootstrap", null, HandleAppRefresh);
        return true;
    }

    public void HandleAppRefresh(BGTask task) 
    {
        BGAppRefreshTask refreshTask = (task as BGAppRefreshTask)!;
        refreshTask.ExpirationHandler += 
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
}
