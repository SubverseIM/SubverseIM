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
                this.RaiseAndSetIfChanged(ref sendMessageText, value);
            }
        }

        public MessagePageViewModel(IServiceManager serviceManager, SubverseContact contact) : base(serviceManager)
        {
            this.contact = contact;

            MessageList = new();
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default) 
        {
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);

            foreach (SubverseMessage message in dbService.GetMessagesWithPeer(contact.OtherPeer).Take(250))
            {
                MessageList.Add(new(peerService.ThisPeer == message.Sender ? null : contact, message));
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

            MessageList.Insert(0, new(null, message));
            dbService.InsertOrUpdateItem(message);

            await peerService.SendMessageAsync(message);
        }
    }
}
