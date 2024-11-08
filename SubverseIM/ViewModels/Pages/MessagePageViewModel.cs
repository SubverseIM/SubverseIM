using SubverseIM.Services;

namespace SubverseIM.ViewModels.Pages
{
    public class MessagePageViewModel : PageViewModelBase
    {
        public override string Title => "Conversation View";

        public MessagePageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
        }
    }
}
