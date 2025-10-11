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
public class MainActivity : AvaloniaMainActivity, ILauncherService
{
    private const int REQUEST_NOTIFICATION_PERMISSION = 1000;

    private class ActivityBackPressedCallback : OnBackPressedCallback
    {
        private readonly IServiceManager? serviceManager;

        public ActivityBackPressedCallback(IServiceManager? serviceManager) : base(true)
        {
            this.serviceManager = serviceManager;
        }

        public override async void HandleOnBackPressed()
        {
            IFrontendService? frontendService = serviceManager is null ? null :
                await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService?.NavigatePreviousView();
        }
    }

    private readonly ServiceConnection<IBootstrapperService> peerServiceConn;

    private readonly CancellationTokenSource cancellationTokenSource;

    private ActivityBackPressedCallback? backPressedCallback;

    private IServiceManager? serviceManager;

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
        peerServiceConn = new();
        cancellationTokenSource = new();
    }

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Platform.Init(this, savedInstanceState);

        Application? app = Application as Application;
        serviceManager = app?.ServiceManager;

        backPressedCallback = new(serviceManager);
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

        serviceManager?.GetOrRegister<ILauncherService>(this);

        serviceManager?.GetOrRegister<IBillingService>(new BillingService());

        serviceManager?.GetOrRegister<IEncryptionService>(new AndroidEncryptionService(this));

        string appDataPath = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ApplicationData
            );

        string dbFilePath = Path.Combine(appDataPath, "SubverseIM.db");
        serviceManager?.GetOrRegister<IDbService>(
            new DbService(dbFilePath)
            );

        if (!peerServiceConn.IsConnected)
        {
            Intent serviceIntent = new Intent(this, typeof(WrappedBootstrapperService));

            BindService(serviceIntent, peerServiceConn, Bind.AutoCreate);
            StartService(serviceIntent);

            serviceManager?.GetOrRegister(
                await peerServiceConn.ConnectAsync()
                );
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        cancellationTokenSource.Dispose();
        if (peerServiceConn.IsConnected)
        {
            UnbindService(peerServiceConn);
        }
        StopService(new Intent(this, typeof(WrappedBootstrapperService)));

        serviceManager?.Dispose();
        System.Environment.Exit(0);
    }

    protected override async void OnStart()
    {
        base.OnStart();
        IsInForeground = true;

        IFrontendService? frontendService = serviceManager is null ? null :
            await serviceManager.GetWithAwaitAsync<IFrontendService>();
        mainTask = frontendService?.RunOnceAsync(cancellationTokenSource.Token);
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

    protected override async void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;

        IFrontendService? frontendService = serviceManager is null ? null :
            await serviceManager.GetWithAwaitAsync<IFrontendService>();
        IDbService? dbService = serviceManager is null ? null :
            await serviceManager.GetWithAwaitAsync<IDbService>();

        IEnumerable<SubverseContact> contacts = (await Task.WhenAll(Intent?
                .GetStringArrayExtra(WrappedBootstrapperService.EXTRA_PARTICIPANTS_ID)?
                .Select(x => dbService?.GetContactAsync(SubversePeerId.FromString(x)))
                .Where(x => x is not null)
                .Cast<Task<SubverseContact?>>() ?? []))
                .Where(x => x is not null)
                .Cast<SubverseContact>();
        string? topicName = Intent?.GetStringExtra(WrappedBootstrapperService.EXTRA_TOPIC_ID);

        if (Intent?.DataString is not null)
        {
            frontendService?.NavigateLaunchedUri();
        }
        else if (contacts.Any())
        {
            frontendService?.NavigateMessageView(contacts, topicName);
        }
        else
        {
            frontendService?.NavigateContactView();
        }
    }

    public byte[]? GetDeviceToken()
    {
        return null;
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

        EditText editText = new(this) { InputType = InputTypes.ClassText, Text = defaultText, ShowSoftInputOnFocus = true };
        frameLayout.AddView(editText);

        AlertDialog? alertDialog = new AlertDialog.Builder(this)
            ?.SetTitle(prompt)
            ?.SetView(frameLayout)
            ?.SetPositiveButton("Submit", (s, ev) => tcs.SetResult(editText.Text))
            ?.SetNegativeButton("Cancel", (s, ev) => tcs.SetResult(null))
            ?.SetCancelable(false)
            ?.Show();
        editText.RequestFocus();

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
        new ShareCompat.IntentBuilder(this)?
            .SetType("text/plain")?
            .SetChooserTitle(title)?
            .SetText(content)?
            .StartChooser();

        return Task.CompletedTask;
    }

    public Task ShareFileToAppAsync(Visual? sender, string title, string path)
    {
        return Task.FromException(new PlatformNotSupportedException("This method is not supported on Android!"));
    }
}
