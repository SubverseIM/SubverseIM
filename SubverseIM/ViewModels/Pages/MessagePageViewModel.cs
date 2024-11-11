using ReactiveUI;
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
        private readonly SubverseContact contact;

        public override string Title => $"Conversation View ({contact.DisplayName})";

        public ObservableCollection<MessageViewModel> MessageList { get; }

        private string? sendMessageText;
        public string? SendMessageText 
        { 
            get => sendMessageText;
            set 
            {
                this.RaiseAndSetIfChanged(ref sendMessageText, value?.Trim());
            }
        }

        public MessagePageViewModel(IServiceManager serviceManager, SubverseContact contact) : base(serviceManager)
        {
            this.contact = contact;

            MessageList = new();
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default) 
        {
            INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>();
            nativeService.ClearNotificationForPeer(contact.OtherPeer);

            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);

            MessageList.Clear();
            foreach (SubverseMessage message in dbService.GetMessagesWithPeer(contact.OtherPeer).Take(250))
            {
                MessageList.Add(new(this, peerService.ThisPeer == message.Sender ? null : contact, message));
            }
        }

        public async Task BackCommandAsync()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView();
        }

        public async Task EditCommandAsync() 
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView(contact);
        }

        public async Task DeleteCommandAsync()
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();

            dbService.DeleteItemById<SubverseContact>(contact.Id);
            frontendService.NavigateContactView();
        }

        public async Task SendCommandAsync() 
        {
            if (string.IsNullOrEmpty(SendMessageText)) return;

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();

            SubverseMessage message = new SubverseMessage()
            {
                Sender = peerService.ThisPeer,
                Recipient = contact.OtherPeer,

                Content = SendMessageText,
                DateSignedOn = DateTime.UtcNow,
            };

            SendMessageText = null;
            await peerService.SendMessageAsync(message);

            MessageList.Insert(0, new(this, null, message));
            dbService.InsertOrUpdateItem(message);
        }
    }
}
