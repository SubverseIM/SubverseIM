using Android.App;
using Android.Content.PM;

using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using SubverseIM.Android.Services;
using SubverseIM.Services;

namespace SubverseIM.Android;

[Activity(
    Label = "SubverseIM.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private readonly ServiceManager<IPeerService> peerServiceManager;

    public MainActivity() 
    {
        peerServiceManager = new();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return AppBuilder.Configure(() => new App(peerServiceManager))
            .WithInterFont()
            .UseReactiveUI();
    }
}
