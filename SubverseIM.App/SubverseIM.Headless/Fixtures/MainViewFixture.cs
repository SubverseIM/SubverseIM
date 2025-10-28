using Avalonia.Controls;
using LiteDB;
using MonoTorrent;
using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Headless.Services;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Faux;
using SubverseIM.ViewModels;
using SubverseIM.Views;
using System.Security.Cryptography;

namespace SubverseIM.Headless.Fixtures;

public class MainViewFixture
{
    public const int EXPECTED_NUM_CONTACTS = 5;

    public const int EXPECTED_NUM_TORRENTS = 5;

    public const string EXPECTED_TOPIC_NAME = "#xunit-testing";

    private TaskCompletionSource<IServiceManager> serviceManagerTcs;

    private TaskCompletionSource<MainViewModel> mainViewModelTcs;

    private TaskCompletionSource<MainView> mainViewTcs;

    private Window? window;

    private bool isInitialized;

    public MainViewFixture()
    {
        serviceManagerTcs = new();
        mainViewModelTcs = new();
        mainViewTcs = new();
    }

    private async Task InitializeAsync()
    {
        IServiceManager serviceManager = new SubverseIM.Services.Implementation.ServiceManager();
        serviceManagerTcs.SetResult(serviceManager);

        await RegisterBootstrapperService(serviceManager);
        await RegisterDbService(serviceManager);
        await RegisterLauncherService(serviceManager);

        MainViewModel mainViewModel = new(serviceManager);
        mainViewModelTcs.SetResult(mainViewModel);

        MainView mainView = new() { DataContext = mainViewModel }; 
        mainViewTcs.SetResult(mainView);

        window = new Window() { Content = mainView };
        window.Show();
    }

    private Task<IBootstrapperService> RegisterBootstrapperService(IServiceManager serviceManager)
    {
        BootstrapperService bootstrapperService = new WrappedBootstrapperService();
        serviceManager.GetOrRegister<IBootstrapperService>(bootstrapperService);

        return Task.FromResult<IBootstrapperService>(bootstrapperService);
    }

    private async Task<IDbService> RegisterDbService(IServiceManager serviceManager)
    {
        IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
        IDbService dbService = serviceManager.GetOrRegister<DbService, IDbService>();

        // Initialize contacts
        List<SubverseContact> contacts = new();
        for (int i = 0; i < EXPECTED_NUM_CONTACTS; i++)
        {
            SubverseContact contact = new SubverseContact
            {
                OtherPeer = new(RandomNumberGenerator.GetBytes(20)),
                DisplayName = "Anonymous",
            };
            contacts.Add(contact);

            await dbService.InsertOrUpdateItemAsync(contact);
        }

        // Initialize messages
        SubversePeerId thisPeer = await bootstrapperService.GetPeerIdAsync();
        SubverseMessage message = new SubverseMessage
        {
            MessageId = new(CallProperties.CreateNewCallId(), thisPeer),

            Sender = thisPeer,
            SenderName = "Anonymous",

            Recipients = contacts.Select(x => x.OtherPeer).ToArray(),
            RecipientNames = contacts.Select(x => x.DisplayName!).ToArray(),

            Content = "This is a test message for the xUnit test suite.",
            TopicName = EXPECTED_TOPIC_NAME,

            DateSignedOn = DateTime.UtcNow,

            WasDecrypted = true,
            WasDelivered = true,
        };
        await dbService.InsertOrUpdateItemAsync(message);

        // Initialize torrents
        for (int i = 0; i < EXPECTED_NUM_TORRENTS; i++)
        {
            InfoHash infoHash = new(RandomNumberGenerator.GetBytes(20));
            MagnetLink magnetLink = new(infoHash, name: "Untitled");

            SubverseTorrent torrent = new(magnetLink.InfoHashes.V1OrV2, magnetLink.ToV1String());
            await dbService.InsertOrUpdateItemAsync(torrent);
        }

        return dbService;
    }

    private Task<ILauncherService> RegisterLauncherService(IServiceManager serviceManager)
    {
        ILauncherService launcherService = serviceManager.GetOrRegister<DefaultLauncherService, ILauncherService>();
        return Task.FromResult(launcherService);
    }

    public Task InitializeOnceAsync()
    {
        lock (this)
        {
            if (isInitialized)
            {
                return Task.CompletedTask;
            }
            else
            {
                isInitialized = true;
                return InitializeAsync();
            }
        }
    }

    public Task<IServiceManager> GetServiceManagerAsync() => serviceManagerTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public Task<MainViewModel> GetViewModelAsync() => mainViewModelTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public Task<MainView> GetViewAsync() => mainViewTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
}
