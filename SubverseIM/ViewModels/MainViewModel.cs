using SubverseIM.Services;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IServiceManager serviceManager;

    public MainViewModel(IServiceManager serviceManager)
    {
        this.serviceManager = serviceManager;
    }

    public string Greeting => serviceManager.GetOrRegister<IPeerService>() is null ? 
        "IPeerService instance was null!" : "IPeerService was registered!";
}
