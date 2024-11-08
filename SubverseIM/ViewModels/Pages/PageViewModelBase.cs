using SubverseIM.Services;

namespace SubverseIM.ViewModels.Pages
{
    public abstract class PageViewModelBase : ViewModelBase
    {
        protected IServiceManager ServiceManager { get; }

        public abstract string Title { get; }

        public PageViewModelBase(IServiceManager serviceManager)
        {
            ServiceManager = serviceManager;
        }
    }
}
