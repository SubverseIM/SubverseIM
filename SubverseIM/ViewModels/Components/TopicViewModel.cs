using SubverseIM.Models;
using SubverseIM.Services;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TopicViewModel : ViewModelBase
    {
        private readonly IServiceManager serviceManager;

        public string TopicName { get; }

        public SubverseContact[] Contacts { get; }

        public TopicViewModel(IServiceManager serviceManager, string topicName, SubverseContact[] contacts) 
        {
            this.serviceManager = serviceManager;

            TopicName = topicName;
            Contacts = contacts;
        }

        public async Task OpenMessageViewCommandAsync() 
        {
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateMessageView(Contacts, TopicName);
        }
    }
}
