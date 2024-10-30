using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
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
    private readonly ServiceManager serviceManager;

    private readonly ServiceConnection<IPeerService> peerServiceConn;

    public MainActivity() 
    {
        serviceManager = new();
        peerServiceConn = new();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        BindService(
            new Intent(this, typeof(WrappedPeerService)),
            peerServiceConn, Bind.AutoCreate
            );

        serviceManager.GetOrRegister(
            peerServiceConn.ConnectAsync().Result
            );
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        UnbindService(peerServiceConn);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return AppBuilder.Configure(
            () => new App(serviceManager)
            ).UseAndroid()
            .WithInterFont()
            .UseReactiveUI();
    }
}
