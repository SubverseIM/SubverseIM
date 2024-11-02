using SubverseIM.Services;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IServiceManager serviceManager;

    public MainViewModel(IServiceManager serviceManager)
    {
        this.serviceManager = serviceManager;
    }
}
