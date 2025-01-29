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

    public MainViewModel GetViewModel() => mainViewModel;

    public MainView GetView() => mainView;

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
