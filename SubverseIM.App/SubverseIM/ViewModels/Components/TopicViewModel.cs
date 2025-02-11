using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Serializers;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System.IO;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class TopicViewModel : ViewModelBase
    {
        private const string DELETE_CONFIRM_TITLE = "Delete topic messages?";
        private const string DELETE_CONFIRM_MESSAGE = "Warning: all messages labeled with this topic will be permanently deleted! Are you sure you want to proceed?";

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

        public async Task ExportTopicCommand()
        {
            TopLevel topLevel = await parent.ServiceManager.GetWithAwaitAsync<TopLevel>();
            IStorageFile? outputFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                DefaultExtension = ".csv",
                SuggestedFileName = $"{TopicName.TrimStart('#')}_exported",
                FileTypeChoices = [new FilePickerFileType("text/csv")]
            });

            if (outputFile is not null) 
            {
                using CsvMessageSerializer serializer = new(await outputFile.OpenWriteAsync());
                IDbService dbService = await parent.ServiceManager.GetWithAwaitAsync<IDbService>();
                dbService.WriteAllMessagesOfTopic(serializer, TopicName);
            }
        }

        public async Task DeleteTopicCommand()
        {
            ILauncherService launcherService = await parent.ServiceManager.GetWithAwaitAsync<ILauncherService>();
            if (await launcherService.ShowConfirmationDialogAsync(DELETE_CONFIRM_TITLE, DELETE_CONFIRM_MESSAGE))
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
