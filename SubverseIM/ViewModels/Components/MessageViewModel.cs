using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class MessageViewModel : ViewModelBase
    {
        private static readonly Regex URL_REGEX = new(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");

        private readonly MessagePageViewModel messagePageView;

        private readonly SubverseContact? fromContact;

        private readonly SubverseMessage innerMessage;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref isSelected, value);
            }
        }

        public string Content => URL_REGEX.Replace(innerMessage.Content ?? string.Empty, "[embed]");

        public string DateString => innerMessage
            .DateSignedOn.ToLocalTime()
            .ToString("dd/MM/yy\nHH:mm:ss");

        public string FromName => fromContact?.DisplayName ?? "You";

        public string FromHeading => FromName + 
            (string.IsNullOrEmpty(innerMessage.TopicName) ? 
            string.Empty : $" ({innerMessage.TopicName})");

        public string CcFooter => innerMessage.RecipientNames.Length > 1 ?
            $"Cc: {string.Join(", ", innerMessage.RecipientNames)}" : string.Empty;

        public string ReadoutText => string.IsNullOrEmpty(innerMessage.TopicName) ?
            $"At {DateString}, {FromName} said: {Content}{(innerMessage.Recipients.Length > 1 ?
                " to " + string.Join(", ", innerMessage.RecipientNames[..^1]) + " and " + innerMessage.RecipientNames[^1] : 
                string.Empty)}" : 
            $"At {DateString}, {FromName} on topic {innerMessage.TopicName} said: {Content}{(innerMessage.Recipients.Length > 1 ?
                " to " + string.Join(", ", innerMessage.RecipientNames[..^1]) + " and " + innerMessage.RecipientNames[^1] : 
                string.Empty)}";

        public SubverseContact[] CcContacts { get; }

        public EmbedViewModel[] Embeds { get; }

        public MessageViewModel(MessagePageViewModel messagePageView, SubverseContact? fromContact, SubverseMessage innerMessage)
        {
            this.messagePageView = messagePageView;
            this.fromContact = fromContact;
            this.innerMessage = innerMessage;

            CcContacts = innerMessage.Recipients
                .Zip(innerMessage.RecipientNames)
                .Select(x => new SubverseContact() 
                { 
                    OtherPeer = x.First,
                    DisplayName = x.Second,
                })
                .ToArray();

            Embeds = URL_REGEX.Matches(
                innerMessage.Content ?? string.Empty
                ).Where(x => x.Success)
                .Select(x => new EmbedViewModel(x.Value))
                .ToArray();
        }

        public async Task DeleteCommandAsync() 
        {
            ILauncherService launcherService = await messagePageView.ServiceManager
                .GetWithAwaitAsync<ILauncherService>();

            if (await launcherService.ShowConfirmationDialogAsync("Delete Message?", "Are you sure you want to delete this message?"))
            {
                IDbService dbService = await messagePageView.ServiceManager
                    .GetWithAwaitAsync<IDbService>();
                dbService.DeleteItemById<SubverseMessage>(innerMessage.Id);
                messagePageView.MessageList.Remove(this);
            }
        }
    }
}
