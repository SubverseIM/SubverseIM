using Android.App;
using Android.Content;
using Android.Content.PM;

using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using SubverseIM.Android.Services;

namespace SubverseIM.Android;

[Activity(
    Label = "SubverseIM.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private readonly ServiceManager serviceManager;

    public MainActivity() 
    {
        serviceManager = new();
    }

    protected override void OnStart()
    {
        base.OnStart();
        BindService(
            new Intent(this, typeof(PeerService)), 
            serviceManager, Bind.AutoCreate
            );
    }

    protected override void OnStop()
    {
        base.OnStop();
        UnbindService(serviceManager);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return AppBuilder.Configure(
            () => new App(serviceManager)
            )
            .UseAndroid()
            .WithInterFont()
            .UseReactiveUI();
    }
}
