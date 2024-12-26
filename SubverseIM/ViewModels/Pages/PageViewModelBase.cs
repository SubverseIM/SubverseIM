using SubverseIM.Services;

namespace SubverseIM.ViewModels.Pages
{
    public abstract class PageViewModelBase : ViewModelBase
    {
        public IServiceManager ServiceManager { get; }

        public abstract string Title { get; }

        public abstract bool HasSidebar { get; }

        public PageViewModelBase(IServiceManager serviceManager)
        {
            ServiceManager = serviceManager;
        }

        public virtual void ToggleSidebarCommand() { }
    }
}
