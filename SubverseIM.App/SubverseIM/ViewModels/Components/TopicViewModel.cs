using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TopicViewModel : ViewModelBase
    {
        private readonly ContactPageViewModel parent;

        public string TopicName { get; }

        public SubverseContact[] Contacts { get; }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref isSelected, value);
            }
        }

        public TopicViewModel(ContactPageViewModel parent, string topicName, SubverseContact[] contacts)
        {
            this.parent = parent;

            TopicName = topicName;
            Contacts = contacts;
        }

        public async Task OpenMessageViewCommand()
        {
            IFrontendService frontendService = await parent.ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateMessageView(Contacts, TopicName);
        }
    }
}
