using Avalonia.Controls;
using LiteDB;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Faux;
using SubverseIM.ViewModels;
using SubverseIM.Views;
using System.Security.Cryptography;

namespace SubverseIM.Headless;

public class MainViewFixture : IDisposable
{
    public const int EXPECTED_NUM_CONTACTS = 5;

    private readonly CancellationTokenSource cts;

    private readonly IServiceManager serviceManager;

    private readonly MainViewModel mainViewModel;

    private readonly MainView mainView;

    private Window? window;

    public MainViewFixture()
    {
        cts = new();

        serviceManager = new Services.Implementation.ServiceManager();

        RegisterBootstrapperService();
        RegisterDbService();
        RegisterLauncherService();

        mainViewModel = new(serviceManager);
        mainView = new() { DataContext = mainViewModel };
    }

    private void RegisterBootstrapperService() 
    {
        BootstrapperService bootstrapperService = new WrappedBootstrapperService();
        serviceManager.GetOrRegister<IBootstrapperService>(bootstrapperService);
    }

    private void RegisterDbService()
    {
        IDbService dbService = serviceManager.GetOrRegister<DbService, IDbService>();
        for (int i = 0; i < EXPECTED_NUM_CONTACTS; i++)
        {
            SubverseContact contact = new SubverseContact
            {
                OtherPeer = new(RandomNumberGenerator.GetBytes(20)),
                DisplayName = "Anonymous",
            };
            dbService.InsertOrUpdateItem(contact);
        }
    }

    private void RegisterLauncherService()
    {
        serviceManager.GetOrRegister<DefaultLauncherService, ILauncherService>();
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
