using Avalonia;
using ReactiveUI;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class ContactPageViewModel : PageViewModelBase<ContactPageViewModel>, IContactContainer
    {
        public override string Title => "Contacts View";

        public override bool HasSidebar => Parent is null;

        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ObservableCollection<TopicViewModel> TopicsList { get; }

        private MessagePageViewModel? parent;
        public MessagePageViewModel? Parent
        {
            get => parent;
            set
            {
                this.RaiseAndSetIfChanged(ref parent, value);
            }
        }

        public ContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            Parent = null;

            ContactsList = new();
            TopicsList = new();
        }

        public async Task LoadContactsAsync(CancellationToken cancellationToken = default)
        {
            ContactsList.Clear();

            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            foreach (SubverseContact contact in dbService.GetContacts())
            {
                ContactViewModel vm = new(ServiceManager, this, contact);
                await vm.LoadPhotoAsync();
                ContactsList.Add(vm);
            }
        }

        public async Task LoadTopicsAsync(CancellationToken cancellationToken = default)
        {
            TopicsList.Clear();

            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            foreach ((string topicName, IEnumerable<SubversePeerId> otherPeers) in dbService.GetAllMessageTopics())
            {
                SubverseContact[] contacts = otherPeers
                    .Select(dbService.GetContact)
                    .Where(x => x is not null)
                    .Cast<SubverseContact>()
                    .ToArray();

                if (contacts.Length > 0) 
                {
                    TopicViewModel vm = new(this, topicName, contacts);
                    TopicsList.Add(vm);
                }
            }
        }

        public async Task InviteCommand(Visual? sender) 
        {
            IBootstrapperService bootstrapperService = await ServiceManager.GetWithAwaitAsync<IBootstrapperService>();
            await bootstrapperService.SendInviteAsync(sender);
        }

        public async Task MessageCommand() 
        {
            IEnumerable<SubverseContact> contacts = ContactsList
                .Where(x => x.IsSelected)
                .Select(x => x.innerContact);

            if (contacts.Any())
            {
                IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
                frontendService.NavigateMessageView(contacts);
            }
            else 
            {
                ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
                await launcherService.ShowAlertDialogAsync("Note", "You must select at least one contact to start a conversation.");
            }
        }

        public async Task OpenFilesCommand()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateTorrentView();
        }

        public async Task OpenSettingsCommand()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateConfigView();
        }

        public async Task OpenProductsCommand()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigatePurchaseView();
        }

        public async Task AddParticipantsCommand()
        {
            Debug.Assert(Parent is not null);
            foreach (SubverseContact contact in ContactsList
                .Where(x => x.IsSelected)
                .Select(x => x.innerContact)
                .ToArray()) 
            {
                Parent.AddUniqueParticipant(contact, true);
            }

            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigatePreviousView();
        }

        public void RemoveContact(ContactViewModel contact)
        {
            ContactsList.Remove(contact);
        }
    }
}
