using Avalonia.Controls;
using SubverseIM.Services;
using SubverseIM.Services.Faux;
using SubverseIM.Services.Implementation;
using SubverseIM.ViewModels;
using SubverseIM.Views;

namespace SubverseIM.Headless;

public class MainViewFixture : IDisposable
{
    private readonly CancellationTokenSource cts;

    private readonly IServiceManager serviceManager;

    private readonly MainViewModel mainViewModel;

    private readonly MainView mainView;

    private Window? window;

    public MainViewFixture()
    {
        cts = new();

        serviceManager = new ServiceManager();
        serviceManager.GetOrRegister<FauxDbService, IDbService>();

        FauxBootstrapperService bootstrapperService = new WrappedFauxBootstrapperService();
        serviceManager.GetOrRegister<IBootstrapperService>(bootstrapperService);

        mainViewModel = new(serviceManager);
        mainView = new() { DataContext = mainViewModel };

        _ = mainViewModel.RunOnceAsync(cts.Token);
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
