using Avalonia;
using ReactiveUI;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class ContactPageViewModel : PageViewModelBase<ContactPageViewModel>, IContactContainer
    {
        public override string Title => "Contacts View";

        public override bool ShouldConfirmBackNavigation => false;

        public override bool HasSidebar => false;

        public ObservableCollection<ContactViewModel> ContactsList { get; }

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
        }

        public async Task LoadContactsAsync(CancellationToken cancellationToken = default)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);

            foreach ((string topicName, IEnumerable<SubversePeerId> participants) in await dbService.GetAllMessageTopicsAsync(cancellationToken))
            {
                SubversePeerId topicId = new(SHA1.HashData(Encoding.UTF8.GetBytes(topicName)));
                SubverseContact newContact =
                    await dbService.GetContactAsync(topicId, cancellationToken) ?? new()
                    {
                        OtherPeer = topicId,
                        TopicName = topicName,
                    };

                IEnumerable<SubverseMessage> messages = await dbService.GetMessagesWithPeersOnTopicAsync(participants.ToHashSet(), topicName, cancellationToken: cancellationToken);
                newContact.DateLastChattedWith = messages.FirstOrDefault()?.DateSignedOn ?? DateTime.MinValue;

                await dbService.InsertOrUpdateItemAsync(newContact, cancellationToken);
            }

            ContactsList.Clear();
            foreach (SubverseContact contact in (await dbService.GetContactsAsync(cancellationToken))
                .Where(x => Parent is null || x.TopicName is null))
            {
                ContactViewModel vm = new(ServiceManager, this, contact);
                await vm.LoadPhotoAsync();
                ContactsList.Add(vm);
            }
        }

        public async Task InviteCommand(Visual? sender)
        {
            IBootstrapperService bootstrapperService = await ServiceManager.GetWithAwaitAsync<IBootstrapperService>();
            await bootstrapperService.SendInviteAsync(sender);
        }

        public async Task MessageCommand()
        {
            IDbService dbService;
            IFrontendService frontendService;
            ILauncherService launcherService;

            IEnumerable<SubverseContact> contacts = ContactsList
                .Where(x => x.IsSelected)
                .Select(x => x.innerContact);

            if (contacts.Any() && contacts.All(x => x.TopicName is null))
            {
                frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
                frontendService.NavigateMessageView(contacts);
            }
            else
            {
                switch (contacts.Count())
                {
                    case 1:
                        dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
                        frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();

                        string topicName = contacts.Single().TopicName!;
                        IEnumerable<SubverseContact> participants = (await Task.WhenAll(
                            (await dbService.GetAllMessageTopicsAsync())[topicName]
                            .Select(x => dbService.GetContactAsync(x))))
                            .Where(x => x is not null)
                            .Cast<SubverseContact>();
                        frontendService.NavigateMessageView(participants, topicName);
                        break;
                    case 0:
                        launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
                        await launcherService.ShowAlertDialogAsync("Note", "You must select at least one contact to start a conversation.");
                        break;
                    default:
                        launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
                        await launcherService.ShowAlertDialogAsync("Note", "You must select exactly one topic to start a conversation.");
                        break;
                }
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
            await frontendService.NavigatePreviousViewAsync(shouldForceNavigation: false);
        }

        public void RemoveContact(ContactViewModel contact)
        {
            ContactsList.Remove(contact);
        }
    }
}
