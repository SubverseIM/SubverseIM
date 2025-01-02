using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System.Collections.ObjectModel;

namespace SubverseIM.ViewModels.Pages
{
    internal class TorrentPageViewModel : PageViewModelBase
    {
        public override string Title => "File Manager";

        public override bool HasSidebar => false;

        public ObservableCollection<TorrentViewModel> Torrents { get; }

        public TorrentPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            Torrents = new();
        }
    }
}
