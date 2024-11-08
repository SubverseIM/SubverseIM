using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using SubverseIM.Android.Services;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android;

[Activity(
    Label = "SubverseIM",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    [Intent.ActionView],
    Label = "Add Contact (SubverseIM)",
    Categories = [
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
        ],
    DataScheme = "sv")]
public class MainActivity : AvaloniaMainActivity<App>, ILauncherService
{
    private readonly ServiceManager serviceManager;

    private readonly ServiceConnection<IPeerService> peerServiceConn;

    public MainActivity()
    {
        serviceManager = new();
        peerServiceConn = new();
    }

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        serviceManager.GetOrRegister<ILauncherService>(this);

        string appDataPath = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ApplicationData
            );
        string dbFilePath = Path.Combine(appDataPath, "SubverseIM.db");
        serviceManager.GetOrRegister<IDbService>(
            new DbService($"Filename={dbFilePath};Password=#FreeTheInternet")
            );

        BindService(
            new Intent(this, typeof(WrappedPeerService)),
            peerServiceConn, Bind.AutoCreate
            );
        serviceManager.GetOrRegister(
            await peerServiceConn.ConnectAsync()
            );
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        UnbindService(peerServiceConn);
        serviceManager.Dispose();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return AppBuilder.Configure(
            () => new App(serviceManager)
            ).UseAndroid()
            .WithInterFont()
            .UseReactiveUI();
    }

    public Uri? GetLaunchedUri()
    {
        return Intent?.DataString is null ?
            null : new Uri(Intent.DataString);
    }

    public Task ShareStringToAppAsync(string title, string content, CancellationToken cancellationToken)
    {
        new ShareCompat.IntentBuilder(this)
            .SetType("text/plain")
            .SetChooserTitle(title)
            .SetText(content)
            .StartChooser();

        return Task.CompletedTask;
    }
}
