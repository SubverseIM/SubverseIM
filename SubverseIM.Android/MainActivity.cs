using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
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
public class MainActivity : AvaloniaMainActivity<App>, INativeService
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

        serviceManager.GetOrRegister<INativeService>(this);

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

    protected override async void OnStart()
    {
        base.OnStart();

        IFrontendService frontendService = await serviceManager
            .GetWithAwaitAsync<IFrontendService>();
        switch (Intent?.Action)
        {
            case Intent.ActionView:
                await frontendService.ViewCreateContactAsync(new(Intent?.Data?.ToString()
                    ?? throw new InvalidOperationException("Intent did not provide a valid URI!")
                    ));
                break;
        }
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

    Task INativeService.ShareStringToAppAsync(string title, string content, CancellationToken cancellationToken)
    {
        new ShareCompat.IntentBuilder(this)
            .SetType("text/plain")
            .SetChooserTitle(title)
            .SetText(content)
            .StartChooser();

        return Task.CompletedTask;
    }

    Task INativeService.SendPushNotificationAsync(string title, string content, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
