using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class MessagePageViewModel : PageViewModelBase, IContactContainer
    {
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
            ContactsList = [..contacts.Select(x => new ContactViewModel(serviceManager, this, x))];
            ContactsList.CollectionChanged += ContactsListChanged;

            MessageList = new();
            TopicsList = new();
        }

        private async void ContactsListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            await InitializeAsync();
        }

        public async Task AddCommandAsync()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView(this);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default) 
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);

            MessageList.Clear();
            foreach (SubverseMessage message in dbService.GetMessagesWithPeersOnTopic(ContactsList.Select(x => x.innerContact.OtherPeer).ToHashSet(), null).Take(250))
            {
                if (!string.IsNullOrEmpty(message.TopicName) && !TopicsList.Contains(message.TopicName)) 
                {
                    TopicsList.Insert(0, message.TopicName);
                }

                if (string.IsNullOrEmpty(SendMessageTopicName) || message.TopicName == SendMessageTopicName)
                {
                    MessageList.Add(new(this, peerService.ThisPeer == message.Sender ? null : 
                        dbService.GetContact(message.Sender) ?? new() 
                        { DisplayName = message.SenderName, OtherPeer = message.Sender }, 
                        message));
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

            string filteredText = SendMessageTopicName ?? string.Empty;
            filteredText = Regex.Replace(filteredText, @"\s+", "-");
            filteredText = Regex.Replace(filteredText, @"[^\w\-]", string.Empty);
            filteredText = Regex.Match(filteredText, @"\#?(\w[\w\-]*\w)").Value;

            if (filteredText.Length > 0)
            {
                SendMessageTopicName = $"#{filteredText.ToLowerInvariant()}";
            }

            if (!string.IsNullOrEmpty(SendMessageTopicName) && !TopicsList.Contains(SendMessageTopicName))
            {
                TopicsList.Insert(0, SendMessageTopicName);
            }

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>();

            SubverseMessage message = new SubverseMessage()
            {
                CallId = CallProperties.CreateNewCallId(),

                TopicName = SendMessageTopicName,

                Sender = peerService.ThisPeer,
                SenderName = "Anonymous",

                Recipients = [..ContactsList.Select(x => x.innerContact.OtherPeer)],
                RecipientNames = [..ContactsList.Select(x => x.innerContact.DisplayName ?? "Anonymous")],

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
