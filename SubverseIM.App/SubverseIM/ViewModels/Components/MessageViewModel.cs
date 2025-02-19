using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class MessageViewModel : ViewModelBase
    {
        private static readonly Regex URL_REGEX = new(
            @"((?:https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b)|(?:magnet:))([-a-zA-Z0-9()@:%_\+.~#?&//=]*)"
            );

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

        public IBrush BubbleBrush { get; }

        public bool IsGroupMessage => innerMessage.RecipientNames.Length > 1;

        public string Content => URL_REGEX.Replace(
            innerMessage.Content ?? string.Empty, "[embed]"
            );

        public string DateString => innerMessage
            .DateSignedOn.ToLocalTime()
            .ToString("dd/MM/yy\nHH:mm");

        public string FromName => fromContact?.DisplayName ?? "You";

        public string FromHeading => FromName + 
            (string.IsNullOrEmpty(innerMessage.TopicName) ? 
            string.Empty : $" ({innerMessage.TopicName})");

        public string CcFooter => $"Cc: {string.Join(", ", innerMessage.RecipientNames)}";

        public string ReadoutText => $"Sent: {DateString}";

        public Task<HorizontalAlignment> ContentAlignmentAsync => GetContentAlignmentAsync();

        public Task<Dock> OptionsAlignmentAsync => GetOptionsAlignmentAsync();

        public SubverseContact[] CcContacts { get; }

        public EmbedViewModel[] Embeds { get; }

        public MessageViewModel(MessagePageViewModel messagePageView, SubverseContact? fromContact, SubverseMessage innerMessage)
        {
            this.messagePageView = messagePageView;
            this.fromContact = fromContact;
            this.innerMessage = innerMessage;

            if(fromContact is null)
            {
                BubbleBrush = new ImmutableSolidColorBrush(messagePageView.DefaultChatColor ?? Colors.MediumPurple);
            }
            else if(fromContact.ChatColorCode == default)
            {
                BubbleBrush = new ImmutableSolidColorBrush(Colors.DimGray);
            }
            else
            {
                BubbleBrush = new ImmutableSolidColorBrush(fromContact.ChatColorCode);
            }

            CcContacts = innerMessage.Recipients
                .Zip(innerMessage.RecipientNames)
                .Select(x => new SubverseContact() 
                { 
                    OtherPeer = x.First,
                    DisplayName = x.Second,
                })
                .ToArray();

            Embeds = URL_REGEX
                .Matches(innerMessage.Content ?? string.Empty)
                .Where(x => x.Success)
                .Select(x => new EmbedViewModel(messagePageView.ServiceManager, x.Value))
                .ToArray();
        }

        private async Task<HorizontalAlignment> GetContentAlignmentAsync() 
        {
            IConfigurationService configurationService = await messagePageView.ServiceManager
                .GetWithAwaitAsync<IConfigurationService>();
            SubverseConfig config = await configurationService.GetConfigAsync();
            return fromContact is null ^ config.MessageMirrorFlag ? 
                HorizontalAlignment.Left : HorizontalAlignment.Right;
        }

        private async Task<Dock> GetOptionsAlignmentAsync()
        {
            IConfigurationService configurationService = await messagePageView.ServiceManager
                .GetWithAwaitAsync<IConfigurationService>();
            SubverseConfig config = await configurationService.GetConfigAsync();
            return fromContact is null ^ config.MessageMirrorFlag ?
                Dock.Right : Dock.Left;
        }

        public async Task DeleteCommand() 
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
