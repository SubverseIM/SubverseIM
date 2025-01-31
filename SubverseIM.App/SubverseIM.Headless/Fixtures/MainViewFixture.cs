using Avalonia.Controls;
using LiteDB;
using SIPSorcery.SIP;
using SubverseIM.Headless.Services;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Faux;
using SubverseIM.ViewModels;
using SubverseIM.Views;
using System.Security.Cryptography;

namespace SubverseIM.Headless.Fixtures;

public class MainViewFixture : IDisposable
{
    public const int EXPECTED_NUM_CONTACTS = 5;

    public const string EXPECTED_TOPIC_NAME = "#xunit-testing";

    private readonly CancellationTokenSource cts;

    private readonly IServiceManager serviceManager;

    private readonly MainViewModel mainViewModel;

    private readonly MainView mainView;

    private Window? window;

    public MainViewFixture()
    {
        cts = new();

        serviceManager = new SubverseIM.Services.Implementation.ServiceManager();

        RegisterBootstrapperService();
        RegisterDbService();
        RegisterLauncherService();

        mainViewModel = new(serviceManager);
        mainView = new() { DataContext = mainViewModel };
    }

    private IBootstrapperService RegisterBootstrapperService() 
    {
        BootstrapperService bootstrapperService = new WrappedBootstrapperService();
        serviceManager.GetOrRegister<IBootstrapperService>(bootstrapperService);

        return bootstrapperService;
    }

    private IDbService RegisterDbService()
    {
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

            dbService.InsertOrUpdateItem(contact);
        }

        // Initialize messages
        SubverseMessage message = new SubverseMessage
        {
            CallId = CallProperties.CreateNewCallId(),

            Sender = new(RandomNumberGenerator.GetBytes(20)),
            SenderName = "Anonymous",

            Recipients = contacts.Select(x => x.OtherPeer).ToArray(),
            RecipientNames = contacts.Select(x => x.DisplayName!).ToArray(),

            Content = "This is a test message for the xUnit test suite.",
            TopicName = EXPECTED_TOPIC_NAME,

            DateSignedOn = DateTime.UtcNow,

            WasDecrypted = true,
            WasDelivered = true,
        };
        dbService.InsertOrUpdateItem(message);

        return dbService;
    }

    private ILauncherService RegisterLauncherService()
    {
        return serviceManager.GetOrRegister<DefaultLauncherService, ILauncherService>();
    }

    public IServiceManager GetServiceManager() => serviceManager;

    public MainViewModel GetViewModel() => mainViewModel;

    public MainView GetView() => mainView;

    public void EnsureWindowShown() 
    {
        if (window is null)
        {
            window = new() { Content = mainView };
            window.Show();
        }
    }

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                cts.Dispose();
                serviceManager.Dispose();
                window?.Close();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
