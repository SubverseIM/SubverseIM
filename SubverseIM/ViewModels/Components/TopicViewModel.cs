using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TopicViewModel : ViewModelBase
    {
        private const string CONFIRM_TITLE = "Delete topic messages?";
        private const string CONFIRM_MESSAGE = "Warning: all messages labeled with this topic will be permanently deleted! Are you sure you want to proceed?";

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

        public async Task DeleteTopicCommand() 
        {
            ILauncherService launcherService = await parent.ServiceManager.GetWithAwaitAsync<ILauncherService>();
            if (await launcherService.ShowConfirmationDialogAsync(CONFIRM_TITLE, CONFIRM_MESSAGE))
            {
                IDbService dbService = await parent.ServiceManager.GetWithAwaitAsync<IDbService>();
                dbService.DeleteAllMessagesOfTopic(TopicName);

                parent.TopicsList.Remove(this);
            }
            else 
            {
                IsSelected = false;
            }
        }
    }
}
