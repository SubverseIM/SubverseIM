using Avalonia.Controls;
using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class MessagePageViewModel : PageViewModelBase, IContactContainer
    {
        private readonly List<ContactViewModel> permContactsList;

        public override string Title => $"Conversation View";

        public override bool HasSidebar => true;

        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ObservableCollection<MessageViewModel> MessageList { get; }

        public ObservableCollection<string> TopicsList { get; }

        private bool isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => isSidebarOpen;
            set
            {
                this.RaiseAndSetIfChanged(ref isSidebarOpen, value);
            }
        }

        private Dock messageTextDock;
        public Dock MessageTextDock
        {
            get => messageTextDock;
            set
            {
                this.RaiseAndSetIfChanged(ref messageTextDock, value);
            }
        }

        private string? sendMessageText;
        public string? SendMessageText
        {
            get => sendMessageText;
            set
            {
                this.RaiseAndSetIfChanged(ref sendMessageText, value?.Trim());
            }
        }

        private string? sendMessageTopicName;
        public string? SendMessageTopicName
        {
            get => sendMessageTopicName;
            set
            {
                this.RaiseAndSetIfChanged(ref sendMessageTopicName, value?.Trim());
            }
        }

        private bool shouldRefreshContacts;
        public bool ShouldRefreshContacts
        {
            get => shouldRefreshContacts;
            set
            {
                this.RaiseAndSetIfChanged(ref shouldRefreshContacts, value);
            }
        }

        public MessagePageViewModel(IServiceManager serviceManager, IEnumerable<SubverseContact> contacts) : base(serviceManager)
        {
            permContactsList = [.. contacts.Select(x => new ContactViewModel(serviceManager, this, x))];
            ContactsList = [.. contacts.Select(x => new ContactViewModel(serviceManager, this, x))];
            MessageList = [];
            TopicsList = [string.Empty];
            MessageTextDock = Dock.Bottom;
        }

        public async Task AddParticipantsCommandAsync()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView(this);
        }

        public async Task AddTopicCommandAsync()
        {
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();

            string filteredText = await launcherService.ShowInputDialogAsync("New topic") ?? string.Empty;
            filteredText = Regex.Replace(filteredText, @"\s+", "-");
            filteredText = Regex.Replace(filteredText, @"[^\w\-]", string.Empty);
            filteredText = Regex.Match(filteredText, @"\#?(\w[\w\-]*\w)").Value;
            filteredText = filteredText.Length > 0 ? $"#{filteredText.ToLowerInvariant()}" : string.Empty;

            if (!string.IsNullOrEmpty(filteredText) && !TopicsList.Contains(filteredText))
            {
                TopicsList.Insert(0, filteredText);
                SendMessageTopicName = filteredText;
            }
        }

        public bool AddUniqueParticipant(SubverseContact newContact, bool permanent)
        {
            IList<ContactViewModel> listToModify = permanent ? permContactsList : ContactsList;
            if (!listToModify.Select(otherContact => otherContact.innerContact)
                .Any(otherContact => newContact.OtherPeer == otherContact.OtherPeer))
            {
                listToModify.Add(new(ServiceManager, this, newContact));
                return true;
            }
            else
            {
                return false;
            }
        }

        public void RemoveContact(ContactViewModel contact)
        {
            permContactsList.Remove(contact);
            ContactsList.Remove(contact);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
            SubversePeerId thisPeer = await peerService.GetPeerIdAsync(cancellationToken);

            MessageList.Clear();

            if (shouldRefreshContacts)
            {
                ContactsList.Clear();
                foreach (ContactViewModel vm in permContactsList)
                {
                    if (vm.innerContact.OtherPeer == thisPeer && permContactsList.Count > 1) continue;

                    ContactsList.Add(vm);
                }
            }
            else
            {
                ShouldRefreshContacts = true;
            }

            HashSet<SubversePeerId> participantIds = permContactsList
                .Select(x => x.innerContact.OtherPeer)
                .ToHashSet();
            foreach (SubverseMessage message in dbService.GetMessagesWithPeersOnTopic(participantIds, null).Take(250))
            {
                if (message.TopicName == "#system") continue;

                if (!string.IsNullOrEmpty(message.TopicName) && !TopicsList.Contains(message.TopicName))
                {
                    string? currentTopicName = SendMessageTopicName;
                    TopicsList.Add(message.TopicName);
                    SendMessageTopicName = currentTopicName;
                }

                SubverseContact sender = dbService.GetContact(message.Sender) ??
                    new() { OtherPeer = message.Sender, DisplayName = message.SenderName, };

                bool isEmptyTopic = string.IsNullOrEmpty(SendMessageTopicName);
                bool isCurrentTopic = message.TopicName == SendMessageTopicName || 
                    (string.IsNullOrEmpty(message.TopicName) && string.IsNullOrEmpty(SendMessageTopicName));
                bool isSentByMe = thisPeer == sender.OtherPeer;

                if (!isEmptyTopic && isCurrentTopic)
                {
                    foreach ((SubversePeerId otherPeer, string contactName) in
                        ((IEnumerable<SubversePeerId>)[message.Sender, .. message.Recipients])
                        .Zip([message.SenderName ?? "Anonymous", .. message.RecipientNames]))
                    {
                        if (otherPeer == thisPeer) continue;

                        SubverseContact participant = dbService.GetContact(otherPeer) ??
                            new() { OtherPeer = otherPeer, DisplayName = contactName, };
                        AddUniqueParticipant(participant, false);
                    }
                }

                if (isCurrentTopic)
                {
                    MessageList.Add(new(this, isSentByMe ? null : sender, message));
                }
            }
        }

        public async Task BackCommandAsync()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView();
        }

        public async Task SendCommandAsync()
        {
            if (string.IsNullOrEmpty(SendMessageText)) return;

            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>();

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            SubversePeerId thisPeer = await peerService.GetPeerIdAsync();
            SubverseContact? thisContact = dbService.GetContact(thisPeer);

            SubverseMessage message = new SubverseMessage()
            {
                CallId = CallProperties.CreateNewCallId(),

                TopicName = SendMessageTopicName,

                Sender = thisPeer,
                SenderName = thisContact?.DisplayName ?? "Anonymous",

                Recipients = [.. ContactsList.Select(x => x.innerContact.OtherPeer)],
                RecipientNames = [.. ContactsList.Select(x => x.innerContact.DisplayName ?? "Anonymous")],

                Content = SendMessageText,
                DateSignedOn = DateTime.UtcNow,
            };

            MessageTextDock = Dock.Bottom;

            MessageList.Insert(0, new(this, null, message));
            dbService.InsertOrUpdateItem(message);

            SendMessageText = null;

            foreach (SubverseContact contact in ContactsList.Select(x => x.innerContact))
            {
                contact.DateLastChattedWith = message.DateSignedOn;
                dbService.InsertOrUpdateItem(contact);

                _ = nativeService.RunInBackgroundAsync(
                    ct => peerService.SendMessageAsync(message, ct)
                    );
            }
        }

        public override void ToggleSidebarCommand()
        {
            base.ToggleSidebarCommand();
            IsSidebarOpen = !IsSidebarOpen;
        }
    }
}
