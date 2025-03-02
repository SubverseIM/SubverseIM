using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DnsClient;
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

        private static readonly Regex[] MARKDOWN_REGEX = [
            new(@"~~([^~]+)~~"), new(@"`([^`]+)`"), new(@"__?([^_]+)__?"), new(@"\*\*{0,2}([^\*]+)\*\*{0,2}"), 
            new(@"(?:(?:\*[^\*]+\*)|(?:\*\*[^\*]+\*\*)|(?:\*\*\*[^\*]+\*\*\*)|(?:_[^_]+_)|(?:__[^_]+__)|(?:~~[^~]+~~)|(?:`[^`]+`))?(.?[^\*_~`]*)"),
            ];

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

        public string ContentString => URL_REGEX.Replace(
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

        public InlineContentViewModel[][] Content { get; }

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

            Content = ContentString.Split('\n')
                .Select(line => MARKDOWN_REGEX
                .SelectMany(x => x.Matches(line))
                .OrderBy(x => x.Groups[1].Index)
                .Where(x => x.Groups[1].Value.Length > 0)
                .Select(x => 
                {
                    if (x.Value.StartsWith("***") && x.Value.EndsWith("***"))
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Emphasis | InlineStyle.Italics);
                    }
                    else if (x.Value.StartsWith("**") && x.Value.EndsWith("**"))
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Emphasis);
                    }
                    else if (x.Value.StartsWith("__") && x.Value.EndsWith("__")) 
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Underline);
                    }
                    else if ((x.Value.StartsWith("_") && x.Value.EndsWith("_")) ||
                        (x.Value.StartsWith("*") && x.Value.EndsWith("*")))
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Italics);
                    }
                    else if (x.Value.StartsWith("~~") && x.Value.EndsWith("~~"))
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Strikeout);
                    }
                    else if (x.Value.StartsWith("`") && x.Value.EndsWith("`"))
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Code);
                    }
                    else
                    {
                        return new InlineContentViewModel(x.Groups[1].Value, InlineStyle.Plain);
                    }
                }).ToArray())
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

        public async Task CopyCommand()
        {
            ILauncherService launcherService = await messagePageView.ServiceManager.GetWithAwaitAsync<ILauncherService>();
            TopLevel topLevel = await messagePageView.ServiceManager.GetWithAwaitAsync<TopLevel>();
            if (topLevel.Clipboard is not null)
            {
                await topLevel.Clipboard.SetTextAsync(innerMessage.Content);
                await launcherService.ShowAlertDialogAsync("Information", "Message content copied to the clipboard.");
            }
            else 
            {
                await launcherService.ShowAlertDialogAsync("Error", "Could not copy message to the clipboard.");
            }
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
