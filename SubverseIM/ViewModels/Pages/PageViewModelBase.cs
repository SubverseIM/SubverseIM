using Avalonia.Controls;
using Avalonia.Platform;
using ReactiveUI;
using SubverseIM.Services;

namespace SubverseIM.ViewModels.Pages
{
    public abstract class PageViewModelBase : ViewModelBase
    {
        public abstract string Title { get; }

        public abstract bool HasSidebar { get; }

        public abstract void OnOrientationChanged(TopLevel? topLevel);

        public abstract void ToggleSidebarCommand();
    }

    public abstract class PageViewModelBase<TSelf> : PageViewModelBase
        where TSelf : PageViewModelBase<TSelf>
    {
        public IServiceManager ServiceManager { get; }

        private bool isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => isSidebarOpen;
            set
            {
                ((TSelf)this).RaiseAndSetIfChanged(ref isSidebarOpen, value);
            }
        }

        private SplitViewDisplayMode sidebarMode;
        public SplitViewDisplayMode SidebarMode
        {
            get => sidebarMode;
            set
            {
                ((TSelf)this).RaiseAndSetIfChanged(ref sidebarMode, value);
            }
        }

        public PageViewModelBase(IServiceManager serviceManager)
        {
            ServiceManager = serviceManager;
        }

        public override void OnOrientationChanged(TopLevel? topLevel)
        {
            bool isLandscape = topLevel?.Screens?.Primary?.CurrentOrientation switch
            {
                ScreenOrientation.Landscape => true,
                ScreenOrientation.LandscapeFlipped => true,
                _ => false
            };

            SidebarMode = isLandscape ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
            IsSidebarOpen = isLandscape;
        }

        public override void ToggleSidebarCommand()
        {
            IsSidebarOpen = !IsSidebarOpen;
        }
    }
}
