using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Serializers;
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
    public class MessagePageViewModel : PageViewModelBase<MessagePageViewModel>, IContactContainer
    {
        private readonly List<ContactViewModel> permContactsList;

        public override string Title => $"Conversation View";

        public override bool HasSidebar => true;

        public Task<bool> IsFormattingAllowedAsync => GetIsFormattingAllowedAsync();
        private async Task<bool> GetIsFormattingAllowedAsync()
        {
            IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>();
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();

            SubverseConfig config = await configurationService.GetConfigAsync();
            return config.IsFormattingAllowed && !launcherService.IsAccessibilityEnabled;
        }

        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ObservableCollection<MessageViewModel> MessageList { get; }

        public ObservableCollection<string> TopicsList { get; }

        public Task<bool> MessageOrderFlagAsync => GetMessageOrderFlagAsync();

        private Color? defaultChatColor;
        public Color? DefaultChatColor
        {
            get => defaultChatColor;
            set
            {
                this.RaiseAndSetIfChanged(ref defaultChatColor, value);
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
        }

        private async Task<bool> GetMessageOrderFlagAsync()
        {
            IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>();
            SubverseConfig config = await configurationService.GetConfigAsync();
            return config.MessageOrderFlag;
        }

        public async Task AddParticipantsCommand()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView(this);
        }

        public async Task AddTopicCommand(string? defaultTopicName = null)
        {
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();

            string filteredText = await launcherService.ShowInputDialogAsync("New topic", defaultTopicName) ?? string.Empty;
            filteredText = Regex.Replace(filteredText, @"\s+", "-");
            filteredText = Regex.Replace(filteredText, @"[^\w\-]", string.Empty);
            filteredText = Regex.Match(filteredText, @"\#?(\w[\w\-]*\w)").Value;
            filteredText = filteredText.Length > 0 ? $"#{filteredText.ToLowerInvariant()}" : string.Empty;

            if (!string.IsNullOrEmpty(filteredText) && !TopicsList.Contains(filteredText) && filteredText != "#system")
            {
                TopicsList.Insert(0, filteredText);
                SendMessageTopicName = filteredText;
            }
        }

        public async Task ExportAllCommand()
        {
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
            if (string.IsNullOrEmpty(SendMessageTopicName))
            {
                await launcherService.ShowAlertDialogAsync("Action Disallowed", "You cannot export the default topic.");
                return;
            }

            TopLevel topLevel = await ServiceManager.GetWithAwaitAsync<TopLevel>();
            IStorageFile? outputFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                DefaultExtension = ".csv",
                SuggestedFileName = $"{SendMessageTopicName.TrimStart('#')}_exported",
                FileTypeChoices = [new FilePickerFileType("text/csv")]
            });

            if (outputFile is not null)
            {
                using CsvMessageSerializer serializer = new(await outputFile.OpenWriteAsync());
                IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
                await dbService.WriteAllMessagesOfTopicAsync(serializer, SendMessageTopicName);
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
            IBootstrapperService bootstrapperService = await ServiceManager.GetWithAwaitAsync<IBootstrapperService>(cancellationToken);
            IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>(cancellationToken);
            IMessageService messageService = await ServiceManager.GetWithAwaitAsync<IMessageService>(cancellationToken);

            SubversePeerId thisPeer = await bootstrapperService.GetPeerIdAsync(cancellationToken);
            HashSet<SubversePeerId> participantIds = permContactsList
                .Select(x => x.innerContact.OtherPeer)
                .ToHashSet();
            if (participantIds.Count > 1 && string.IsNullOrEmpty(SendMessageTopicName))
            {
                TopicsList.Remove(string.Empty);
                await AddTopicCommand();

                if (string.IsNullOrEmpty(SendMessageTopicName)) 
                {
                    Dispatcher.UIThread.Post(() => frontendService.NavigatePreviousView());
                    return;
                }
            }
            else if (participantIds.Count == 1 && !TopicsList.Contains(string.Empty))
            {
                TopicsList.Insert(0, string.Empty);
            }

            SubverseConfig config = await configurationService.GetConfigAsync(cancellationToken);
            DefaultChatColor = config.DefaultChatColorCode is null ?
                null : Color.FromUInt32(config.DefaultChatColorCode.Value);

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

            MessageList.Clear();
            foreach (SubverseMessage message in await dbService.GetMessagesWithPeersOnTopicAsync(participantIds, null, config.MessageOrderFlag, cancellationToken))
            {
                if (message.TopicName == "#system") continue;

                if (!string.IsNullOrEmpty(message.TopicName) && !TopicsList.Contains(message.TopicName))
                {
                    string? currentTopicName = SendMessageTopicName;
                    TopicsList.Add(message.TopicName);
                    SendMessageTopicName = currentTopicName;
                }

                SubverseContact sender = await dbService.GetContactAsync(message.Sender, cancellationToken) ??
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

                        SubverseContact participant = await dbService.GetContactAsync(otherPeer, cancellationToken) ??
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

        private async Task SendMessageAsync(string? messageText = null, string? messageTopicName = null)
        {
            messageText ??= SendMessageText;
            messageTopicName ??= SendMessageTopicName;

            if (string.IsNullOrEmpty(messageText)) return;

            IBootstrapperService peerService = await ServiceManager.GetWithAwaitAsync<IBootstrapperService>();
            IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>();
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>();
            IMessageService messageService = await ServiceManager.GetWithAwaitAsync<IMessageService>();

            SubversePeerId thisPeer = await peerService.GetPeerIdAsync();
            SubverseContact? thisContact = await dbService.GetContactAsync(thisPeer);
            SubverseConfig config = await configurationService.GetConfigAsync();

            SubverseMessage message = new SubverseMessage()
            {
                MessageId = new(CallProperties.CreateNewCallId(), thisPeer),

                TopicName = messageTopicName,

                Sender = thisPeer,
                SenderName = thisContact?.DisplayName ?? "Anonymous",

                Recipients = [.. ContactsList.Select(x => x.innerContact.OtherPeer)],
                RecipientNames = [.. ContactsList.Select(x => x.innerContact.DisplayName ?? "Anonymous")],

                Content = messageText,

                DateSignedOn = DateTime.UtcNow,
            };

            await dbService.InsertOrUpdateItemAsync(message);

            if (messageText == SendMessageText)
            {
                SendMessageText = null;
            }

            if (messageTopicName == SendMessageTopicName && config.MessageOrderFlag == false)
            {
                MessageList.Insert(0, new(this, null, message));
            }
            else if (messageTopicName == SendMessageTopicName && config.MessageOrderFlag == true)
            {
                MessageList.Add(new(this, null, message));
            }

            foreach (SubverseContact contact in permContactsList.Select(x => x.innerContact))
            {
                contact.DateLastChattedWith = message.DateSignedOn;
                await dbService.InsertOrUpdateItemAsync(contact);

                _ = nativeService.RunInBackgroundAsync(
                    ct => messageService.SendMessageAsync(message, cancellationToken: ct)
                    );
            }
        }

        public Task SendCommand()
        {
            return SendMessageAsync();
        }

        public async Task AttachFileCommand()
        {
            ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>();

            TopLevel topLevel = await ServiceManager.GetWithAwaitAsync<TopLevel>();
            IStorageFile? selectedFile = (await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "Select file attachment",
                    FileTypeFilter = [FilePickerFileTypes.All]
                })).SingleOrDefault();
            if (selectedFile is not null)
            {
                SubverseTorrent torrent = await torrentService.AddTorrentAsync(selectedFile);
                await SendMessageAsync(torrent.MagnetUri);
            }
        }
    }
}
