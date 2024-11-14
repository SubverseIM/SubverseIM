using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class MessagePageViewModel : PageViewModelBase
    {
        internal readonly SubverseContact[] contacts;

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

        public MessagePageViewModel(IServiceManager serviceManager, SubverseContact[] contacts) : base(serviceManager)
        {
            this.contacts = contacts;

            ContactsList = new(contacts.Select(x => new ContactViewModel(serviceManager, x)));
            MessageList = new();
            TopicsList = new();
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default) 
        {
            INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>();
            foreach (SubverseContact contact in contacts)
            {
                nativeService.ClearNotificationForPeer(contact.OtherPeer);
            }

            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);

            MessageList.Clear();
            foreach (SubverseMessage message in dbService.GetMessagesWithPeersOnTopic(contacts.Select(x => x.OtherPeer), null).Take(250))
            {
                if (message.TopicName is not null && !TopicsList.Contains(message.TopicName)) 
                {
                    TopicsList.Add(message.TopicName);
                }

                if (string.IsNullOrEmpty(SendMessageTopicName) || message.TopicName == SendMessageTopicName)
                {
                    MessageList.Add(new(this, peerService.ThisPeer == message.Sender ? null :
                        contacts.Single(x => x.OtherPeer == message.Sender), message));
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

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();

            string callId = CallProperties.CreateNewCallId();
            foreach (SubverseContact contact in contacts) 
            {
                SubverseMessage message = new SubverseMessage()
                {
                    CallId = callId,

                    TopicName = SendMessageTopicName,

                    Sender = peerService.ThisPeer,
                    Recipient = contact.OtherPeer,

                    Content = SendMessageText,
                    DateSignedOn = DateTime.UtcNow,
                };

                MessageList.Insert(0, new(this, null, message));
                dbService.InsertOrUpdateItem(message);

                SendMessageText = null;
                _ = peerService.SendMessageAsync(message);
            }
        }
    }
}
