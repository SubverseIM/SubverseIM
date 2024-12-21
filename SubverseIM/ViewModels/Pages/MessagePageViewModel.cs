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
        private readonly SubverseContact[] contacts;

        public override string Title => $"Conversation View";

        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ObservableCollection<MessageViewModel> MessageList { get; }

        public ObservableCollection<string> TopicsList { get; }

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

        public MessagePageViewModel(IServiceManager serviceManager, IEnumerable<SubverseContact> contacts) : base(serviceManager)
        {
            this.contacts = contacts.ToArray();
            ContactsList = [.. contacts.Select(x => new ContactViewModel(serviceManager, this, x))];
            MessageList = [];
            TopicsList = [string.Empty];
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

        public bool AddUniqueParticipant(SubverseContact newContact)
        {
            if (!ContactsList.Select(otherContact => otherContact.innerContact)
                .Any(otherContact => newContact.OtherPeer == otherContact.OtherPeer))
            {
                ContactsList.Add(new(ServiceManager, this, newContact));
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
            SubversePeerId thisPeer = await peerService.GetPeerIdAsync(cancellationToken);

            ContactsList.Clear();
            MessageList.Clear();

            foreach (SubverseMessage message in dbService.GetMessagesWithPeersOnTopic(
                contacts.Select(x => x.OtherPeer).ToHashSet(), null).Take(250))
            {
                if (!string.IsNullOrEmpty(message.TopicName) && !TopicsList.Contains(message.TopicName))
                {
                    string? currentTopicName = SendMessageTopicName;
                    TopicsList.Add(message.TopicName);
                    SendMessageTopicName = currentTopicName;
                }


                SubverseContact sender = dbService.GetContact(message.Sender) ?? 
                    new() { OtherPeer = message.Sender, DisplayName = message.SenderName, };

                bool isEmptyTopic = string.IsNullOrEmpty(SendMessageTopicName);
                bool isCurrentTopic = message.TopicName == SendMessageTopicName;
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
                        AddUniqueParticipant(participant);
                    }
                }
                else if (isEmptyTopic)
                {
                    foreach (SubverseContact contact in contacts)
                    {
                        if (contact.OtherPeer == thisPeer && contacts.Length > 1) continue;

                        AddUniqueParticipant(contact);
                    }
                }

                if (isEmptyTopic || isCurrentTopic)
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

            MessageList.Insert(0, new(this, null, message));
            dbService.InsertOrUpdateItem(message);

            foreach (SubverseContact contact in ContactsList.Select(x => x.innerContact))
            {
                contact.DateLastChattedWith = message.DateSignedOn;
                dbService.InsertOrUpdateItem(contact);

                SendMessageText = null;
                _ = nativeService.RunInBackgroundAsync(
                    ct => peerService.SendMessageAsync(message, ct)
                    );
            }
        }
    }
}
