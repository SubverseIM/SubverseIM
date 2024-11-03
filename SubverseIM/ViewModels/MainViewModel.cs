using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System.Collections.Generic;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IServiceManager serviceManager;

    private readonly ContactPageViewModel contactPage;

    private readonly Dictionary<SubversePeerId, MessagePageViewModel> messagePageMap;

    private PageViewModelBase currentPage;

    public PageViewModelBase CurrentPage
    {
        get { return currentPage; }
        private set { this.RaiseAndSetIfChanged(ref currentPage, value); }
    }

    public MainViewModel(IServiceManager serviceManager)
    {
        this.serviceManager = serviceManager;

        contactPage = new(serviceManager);
        messagePageMap = new();

        currentPage = contactPage;
    }
}
