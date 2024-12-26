using Avalonia;
using ReactiveUI;
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
    public class ContactPageViewModel : PageViewModelBase, IContactContainer
    {
        public override string Title => "Contacts View";

        public override bool HasSidebar => IsNotDialog;

        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ObservableCollection<TopicViewModel> TopicsList { get; }

        private bool isDialog;
        public bool IsDialog
        {
            get => isDialog;
            private set
            {
                IsNotDialog = !value;
                this.RaiseAndSetIfChanged(ref isDialog, value);
            }
        }

        private bool isNotDialog;
        public bool IsNotDialog
        {
            get => isNotDialog;
            private set
            {
                this.RaiseAndSetIfChanged(ref isNotDialog, value);
                this.RaisePropertyChanged(nameof(HasSidebar));
            }
        }

        private bool isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => isSidebarOpen;
            set
            {
                this.RaiseAndSetIfChanged(ref isSidebarOpen, value);
            }
        }

        private MessagePageViewModel? parent;
        public MessagePageViewModel? Parent
        {
            get => parent;
            set
            {
                IsDialog = value is not null;
                this.RaiseAndSetIfChanged(ref parent, value);
            }
        }

        public ContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            Parent = null;

            ContactsList = new();
            TopicsList = new();
        }

        public void RemoveContact(ContactViewModel contact)
        {
            ContactsList.Remove(contact);
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
                TopicViewModel vm = new(
                    ServiceManager, topicName, otherPeers
                    .Select(dbService.GetContact)
                    .Where(x => x is not null)
                    .Cast<SubverseContact>()
                    .ToArray()
                    );
                TopicsList.Add(vm);
            }
        }

        public async Task InviteCommandAsync(Visual? sender) 
        {
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            await peerService.SendInviteAsync(sender);
        }

        public async Task MessageCommandAsync() 
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

        public async Task AddParticipantsCommandAsync()
        {
            Debug.Assert(Parent is not null);
            foreach (SubverseContact contact in ContactsList
                .Where(x => x.IsSelected)
                .Select(x => x.innerContact)) 
            {
                Parent.AddUniqueParticipant(contact, true);
            }

            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigatePreviousView();
        }

        public override void ToggleSidebarCommand()
        {
            base.ToggleSidebarCommand();
            IsSidebarOpen = !IsSidebarOpen;
        }
    }
}
