using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Views.Accessibility;
using Android.Widget;
using AndroidX.Activity;
using AndroidX.Core.App;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using Java.Lang;
using Microsoft.Maui.ApplicationModel;
using SubverseIM.Android.Services;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android;

[Activity(
    Label = "SubverseIM",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.FullUser,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.Orientation,
    LaunchMode = LaunchMode.SingleInstance)]
[IntentFilter(
    [Intent.ActionView],
    Label = "Add Contact (SubverseIM)",
    Categories = [
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
        ],
    DataScheme = "sv")]
[IntentFilter(
    [Intent.ActionView],
    Label = "Add Torrent (SubverseIM)",
    Categories = [Intent.CategoryDefault],
    DataScheme = "magnet")]
public class MainActivity : AvaloniaMainActivity<App>, ILauncherService
{
    private const int REQUEST_NOTIFICATION_PERMISSION = 1000;

    private class ActivityBackPressedCallback : OnBackPressedCallback
    {
        private readonly IServiceManager serviceManager;

        public ActivityBackPressedCallback(IServiceManager serviceManager) : base(true)
        {
            this.serviceManager = serviceManager;
        }

        public override async void HandleOnBackPressed()
        {
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigatePreviousView();
        }
    }

    private readonly ServiceManager serviceManager;

    private readonly ServiceConnection<IBootstrapperService> peerServiceConn;

    private readonly CancellationTokenSource cancellationTokenSource;

    private readonly ActivityBackPressedCallback backPressedCallback;

    private Task? mainTask;

    public bool NotificationsAllowed { get; private set; }

    public bool IsInForeground { get; private set; }

    public bool IsAccessibilityEnabled 
    { 
        get 
        {
            AccessibilityManager am = (AccessibilityManager)GetSystemService(AccessibilityService)!;
            return am.IsTouchExplorationEnabled;
        } 
    }

    public MainActivity()
    {
        serviceManager = new();
        peerServiceConn = new();
        cancellationTokenSource = new();

        backPressedCallback = new(serviceManager);
    }

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Platform.Init(this, savedInstanceState);

        OnBackPressedDispatcher.AddCallback(backPressedCallback);

        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            CheckSelfPermission(Manifest.Permission.PostNotifications) == Permission.Denied)
        {
            RequestPermissions([Manifest.Permission.PostNotifications], REQUEST_NOTIFICATION_PERMISSION);
        }
        else
        {
            NotificationsAllowed = true;
        }

        serviceManager.GetOrRegister<ILauncherService>(this);

        serviceManager.GetOrRegister<IBillingService>(new BillingService());

        string appDataPath = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ApplicationData
            );

        string dbFilePath = Path.Combine(appDataPath, "SubverseIM.db");
        serviceManager.GetOrRegister<IDbService>(
            new DbService($"Filename={dbFilePath};Password=#FreeTheInternet")
            );

        if (!peerServiceConn.IsConnected)
        {
            Intent serviceIntent = new Intent(this, typeof(WrappedBootstrapperService));

            BindService(serviceIntent, peerServiceConn, Bind.AutoCreate);
            StartService(serviceIntent);

            serviceManager.GetOrRegister(
                await peerServiceConn.ConnectAsync()
                );
        }
    }

    protected override async void OnDestroy()
    {
        base.OnDestroy();

        cancellationTokenSource.Dispose();
        serviceManager.Dispose();

        if (peerServiceConn.IsConnected)
        {
            UnbindService(peerServiceConn);
            StopService(new Intent(this, typeof(WrappedBootstrapperService)));
        }

        try
        {
            if (mainTask is not null) 
            { await mainTask; }
        }
        catch (System.OperationCanceledException) { }

        System.Environment.Exit(0);
    }

    protected override async void OnStart()
    {
        base.OnStart();
        IsInForeground = true;

        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        mainTask = frontendService.RunOnceAsync(cancellationTokenSource.Token);
    }

    protected override void OnStop()
    {
        base.OnStop();
        IsInForeground = false;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        switch (requestCode)
        {
            case REQUEST_NOTIFICATION_PERMISSION:
                NotificationsAllowed = grantResults.All(x => x == Permission.Granted);
                break;
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return AppBuilder.Configure(
            () => new App(serviceManager)
            ).UseAndroid()
            .WithInterFont()
            .UseReactiveUI();
    }

    protected override async void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;

        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

        IEnumerable<SubverseContact>? contacts = Intent?
                .GetStringArrayExtra(WrappedBootstrapperService.EXTRA_PARTICIPANTS_ID)?
                .Select(x => dbService.GetContact(SubversePeerId.FromString(x)))
                .Where(x => x is not null)
                .Cast<SubverseContact>();
        string? topicName = Intent?.GetStringExtra(WrappedBootstrapperService.EXTRA_TOPIC_ID);

        if (Intent?.DataString is not null)
        {
            frontendService.NavigateLaunchedUri();
        }
        else if (contacts is not null)
        {
            frontendService.NavigateMessageView(contacts, topicName);
        }
        else 
        {
            frontendService.NavigateContactView();
        }
    }

    public Uri? GetLaunchedUri()
    {
        return Intent?.DataString is null ?
            null : new Uri(Intent.DataString);
    }

    public Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        TaskCompletionSource<bool> tcs = new();

        AlertDialog? alertDialog = new AlertDialog.Builder(this)
            ?.SetTitle(title)
            ?.SetMessage(message)
            ?.SetPositiveButton("Yes", (s, ev) => tcs.SetResult(true))
            ?.SetNegativeButton("No", (s, ev) => tcs.SetResult(false))
            ?.Show();

        return tcs.Task;
    }

    public Task ShowAlertDialogAsync(string title, string message)
    {
        TaskCompletionSource tcs = new();

        AlertDialog? alertDialog = new AlertDialog.Builder(this)
            ?.SetTitle(title)
            ?.SetMessage(message)
            ?.SetNeutralButton("Ok", (s, ev) => tcs.SetResult())
            ?.Show();

        return tcs.Task;
    }

    public Task<string?> ShowInputDialogAsync(string prompt, string? defaultText)
    {
        TaskCompletionSource<string?> tcs = new();

        FrameLayout frameLayout = new(this);
        frameLayout.SetPadding(25, 25, 25, 25);

        EditText editText = new(this) { InputType = InputTypes.ClassText, Text = defaultText };
        frameLayout.AddView(editText);

        AlertDialog? alertDialog = new AlertDialog.Builder(this)
            ?.SetTitle(prompt)
            ?.SetView(frameLayout)
            ?.SetPositiveButton("Submit", (s, ev) => tcs.SetResult(editText.Text))
            ?.SetNegativeButton("Cancel", (s, ev) => tcs.SetResult(null))
            ?.SetCancelable(false)
            ?.Show();

        return tcs.Task;
    }

    public Task<string?> ShowPickerDialogAsync(string prompt, string? defaultItem = null, params string[] items)
    {
        TaskCompletionSource<string?> tcs = new();

        FrameLayout frameLayout = new(this);
        frameLayout.SetPadding(25, 25, 25, 25);

        ArrayAdapter<ICharSequence> adapter = new(this,
            Resource.Layout.support_simple_spinner_dropdown_item,
            CharSequence.ArrayFromStringArray(items)
            );
        Spinner spinner = new(this) { Adapter = adapter };
        frameLayout.AddView(spinner);

        AlertDialog? alertDialog = new AlertDialog.Builder(this)
            ?.SetTitle(prompt)
            ?.SetView(frameLayout)
            ?.SetPositiveButton("Submit", (s, ev) => tcs.SetResult(((ICharSequence?)spinner.SelectedItem)?.ToString()))
            ?.SetNegativeButton("Cancel", (s, ev) => tcs.SetResult(null))
            ?.SetCancelable(false)
            ?.Show();

        return tcs.Task;
    }

    public Task ShareUrlToAppAsync(Visual? sender, string title, string content)
    {
        new ShareCompat.IntentBuilder(this)
            .SetType("text/plain")
            .SetChooserTitle(title)
            .SetText(content)
            .StartChooser();

        return Task.CompletedTask;
    }

    public Task ShareFileToAppAsync(Visual? sender, string title, string path)
    {
        return Task.FromException(new PlatformNotSupportedException("This method is not supported on Android!"));
    }
}
