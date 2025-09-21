using Avalonia.Controls;
using Avalonia.Threading;
using LiteDB;
using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase, IFrontendService
{
    private const string DONATION_PROMPT_TITLE = "Support us?";

    private const string DONATION_PROMPT_MESSAGE = "If you're enjoying using our app, please consider a one-time donation. Doing so would help support future development of this app and permanently disable these prompts on your devices. Would you like to donate now?";

    private readonly IServiceManager serviceManager;

    private readonly ContactPageViewModel contactPage;

    private readonly CreateContactPageViewModel createContactPage;

    private readonly TorrentPageViewModel torrentPage;

    private readonly ConfigPageViewModel configPage;

    private readonly PurchasePageViewModel purchasePage;

    private Stack<PageViewModelBase> previousPages;

    private PageViewModelBase currentPage;

    private Task? mainTask;

    public Action? ScreenOrientationChangedDelegate { get; set; }

    public PageViewModelBase CurrentPage
    {
        get { return currentPage; }
        private set
        {
            if (HasPreviousView || value != contactPage)
            {
                previousPages.Push(currentPage);
                HasPreviousView = true;
            }

            this.RaiseAndSetIfChanged(ref currentPage, value);
            ScreenOrientationChangedDelegate?.Invoke();
        }
    }

    private bool hasPreviousView;
    public bool HasPreviousView
    {
        get => hasPreviousView;
        private set
        {
            this.RaiseAndSetIfChanged(ref hasPreviousView, value);
        }
    }

    public MainViewModel(IServiceManager serviceManager)
    {
        this.serviceManager = serviceManager;
        serviceManager.GetOrRegister<IFrontendService>(this);

        contactPage = new(serviceManager);
        createContactPage = new(serviceManager);
        torrentPage = new(serviceManager);
        configPage = new(serviceManager);
        purchasePage = new(serviceManager);

        previousPages = new();
        currentPage = contactPage;
    }

    public async Task RunOnceBackgroundAsync()
    {
        if (mainTask?.IsCompleted ?? true)
        {
            INativeService nativeService = await serviceManager.GetWithAwaitAsync<INativeService>();
            mainTask = nativeService.RunInBackgroundAsync(RunAsync);
        }

        await mainTask;
    }

    public Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (mainTask?.IsCompleted ?? true)
        {
            return mainTask = Task.Run(() => RunAsync(cancellationToken));
        }
        else
        {
            return mainTask;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
        IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);
        IMessageService messageService = await serviceManager.GetWithAwaitAsync<IMessageService>(cancellationToken);
        INativeService nativeService = await serviceManager.GetWithAwaitAsync<INativeService>(cancellationToken);

        SubversePeerId thisPeer = await bootstrapperService.GetPeerIdAsync(cancellationToken);
        SubverseContact? thisContact = dbService.GetContact(thisPeer);

        List<Task> subTasks =
        [
            Task.Run(Task? () => messageService.ProcessRelayAsync(cancellationToken)),
            Task.Run(Task? () => bootstrapperService.BootstrapSelfAsync(cancellationToken)),
            Task.Run(torrentPage.InitializeAsync),
        ];

        int unsentCount = 0, joinCount = 0;
        foreach (SubverseMessage message in dbService.GetAllUndeliveredMessages())
        {
            subTasks.Add(Task.Run(async Task? () =>
            {
                await Task.Delay(++unsentCount * 333);
                await messageService.SendMessageAsync(message, cancellationToken);
            }));
        }

        lock (messageService.CachedPeers)
        {
            foreach (SubverseContact contact in dbService.GetContacts())
            {
                messageService.CachedPeers.TryAdd(
                    contact.OtherPeer,
                    new SubversePeer
                    {
                        OtherPeer = contact.OtherPeer
                    });

                SubverseMessage message = new SubverseMessage()
                {
                    MessageId = new(CallProperties.CreateNewCallId(), contact.OtherPeer),

                    TopicName = "#system",

                    Sender = thisPeer,
                    SenderName = thisContact?.DisplayName ?? "Anonymous",

                    Recipients = [contact.OtherPeer],
                    RecipientNames = [contact.DisplayName ?? "Anonymous"],

                    Content = "<joined SubverseIM>",
                    DateSignedOn = DateTime.UtcNow,
                };

                subTasks.Add(Task.Run(async Task? () =>
                {
                    await Task.Delay(++joinCount * 333);
                    await messageService.SendMessageAsync(message, cancellationToken);
                }));
            }
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SubverseMessage message = await messageService.ReceiveMessageAsync(cancellationToken);

                SubversePeerId? topicId = message.TopicName is null || message.TopicName == "#system" ?
                    null : new(SHA1.HashData(Encoding.UTF8.GetBytes(message.TopicName)));
                string? topicName = message.TopicName is null || message.TopicName == "#system" ? 
                    null : message.TopicName;

                SubverseContact contact = dbService.GetContact(topicId ?? message.Sender) ??
                    new SubverseContact()
                    {
                        OtherPeer = topicId ?? message.Sender,
                        DisplayName = topicName ?? message.SenderName,
                        TopicName = topicName,
                    };

                contact.DateLastChattedWith = message.DateSignedOn;
                dbService.InsertOrUpdateItem(contact);

                lock (messageService.CachedPeers)
                {
                    messageService.CachedPeers.TryAdd(
                        contact.OtherPeer,
                        new SubversePeer
                        {
                            OtherPeer = contact.OtherPeer
                        });
                }

                try
                {
                    dbService.InsertOrUpdateItem(message);
                    await contactPage.LoadContactsAsync(cancellationToken);

                    bool isCurrentPeer = false;
                    if (contact is not null && currentPage is MessagePageViewModel vm &&
                        (isCurrentPeer = vm.ContactsList.Any(x => x.innerContact.OtherPeer == message.Sender) &&
                        message.TopicName != "#system" && (message.WasDecrypted ?? true) && (message.TopicName == vm.SendMessageTopicName ||
                        (string.IsNullOrEmpty(message.TopicName) && string.IsNullOrEmpty(vm.SendMessageTopicName))
                        )))
                    {
                        if (!string.IsNullOrEmpty(message.TopicName) &&
                            !vm.TopicsList.Contains(message.TopicName))
                        {
                            vm.TopicsList.Insert(0, message.TopicName);
                        }

                        SubverseContact sender = dbService.GetContact(message.Sender) ?? new()
                        {
                            OtherPeer = message.Sender,
                            DisplayName = message.SenderName
                        };

                        MessageViewModel messageViewModel = new(vm, sender, message);
                        foreach (SubverseContact participant in messageViewModel.CcContacts)
                        {
                            if (participant.OtherPeer == thisPeer) continue;
                            vm.AddUniqueParticipant(participant, false);
                        }

                        SubverseConfig config = await configurationService.GetConfigAsync();
                        if (config.MessageMirrorFlag == false)
                        {
                            vm.MessageList.Insert(0, messageViewModel);
                        }
                        else
                        {
                            vm.MessageList.Add(messageViewModel);
                        }
                    }

                    if (launcherService.NotificationsAllowed &&
                        (!launcherService.IsInForeground ||
                        launcherService.IsAccessibilityEnabled ||
                        !isCurrentPeer) && (message.WasDecrypted ?? true))
                    {
                        await nativeService.SendPushNotificationAsync(serviceManager, message);
                    }
                    else if (launcherService.NotificationsAllowed)
                    {
                        nativeService.ClearNotification(message);
                    }
                }
                catch (LiteException ex) when (ex.ErrorCode == LiteException.INDEX_DUPLICATE_KEY) { }
            }
        }
        finally 
        {
            await torrentPage.DestroyAsync();
            await Task.WhenAll(subTasks);
        }
    }

    public async Task RestorePurchasesAsync()
    {
        IBillingService billingService = await serviceManager.GetWithAwaitAsync<IBillingService>();
        IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();

        SubverseConfig config = await configurationService.GetConfigAsync();
        if (await billingService.WasAnyItemPurchasedAsync(["donation_small", "donation_normal", "donation_large"]))
        {
            config.DateLastPrompted = DateTime.UtcNow;
            config.PromptFreqIndex = null;

            await configurationService.PersistConfigAsync();
        }
    }

    public async Task PromptForPurchaseAsync()
    {
        IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();

        SubverseConfig config = await configurationService.GetConfigAsync();
        TimeSpan promptInterval = configurationService.GetPromptFrequencyValueFromIndex(config.PromptFreqIndex);
        if (config.DateLastPrompted == default)
        {
            config.DateLastPrompted = DateTime.UtcNow;
            config.PromptFreqIndex = 0;

            await configurationService.PersistConfigAsync();
        }
        else if (DateTime.UtcNow - config.DateLastPrompted > promptInterval)
        {
            config.DateLastPrompted = DateTime.UtcNow;
            await configurationService.PersistConfigAsync();

            if (await launcherService.ShowConfirmationDialogAsync(DONATION_PROMPT_TITLE, DONATION_PROMPT_MESSAGE))
            {
                NavigatePurchaseView();
            }
        }
    }

    public bool NavigatePreviousView()
    {
        if (previousPages.TryPop(out PageViewModelBase? previousPage))
        {
            this.RaiseAndSetIfChanged(ref currentPage, previousPage, nameof(CurrentPage));
            HasPreviousView = previousPages.Count > 0;
            return true;
        }
        else
        {
            return false;
        }
    }

    public async void NavigateLaunchedUri(Uri? overrideUri = null)
    {
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
        ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
        Uri? launchedUri = overrideUri ?? launcherService.GetLaunchedUri();

        switch (launchedUri?.Scheme)
        {
            case "sv":
                await createContactPage.InitializeAsync(launchedUri);
                CurrentPage = createContactPage;
                break;
            case "magnet":
                await torrentService.AddTorrentAsync(launchedUri.OriginalString);
                CurrentPage = torrentPage;
                break;
            case null:
                CurrentPage = contactPage;
                break;
        }

        // Synchronize purchases
        await RestorePurchasesAsync();
        await PromptForPurchaseAsync();
    }

    public void NavigateContactView(MessagePageViewModel? parentOrNull)
    {
        contactPage.Parent = parentOrNull;
        contactPage.IsSidebarOpen = false;

        CurrentPage = contactPage;
    }

    public async void NavigateContactView(SubverseContact contact)
    {
        if (CurrentPage is MessagePageViewModel messagePageViewModel)
        {
            messagePageViewModel.ShouldRefreshContacts = false;
        }

        await createContactPage.InitializeAsync(new Uri($"sv://{contact.OtherPeer}"));
        CurrentPage = createContactPage;
    }

    public async void NavigateMessageView(IEnumerable<SubverseContact> contacts, string? topicName)
    {
        MessagePageViewModel vm = new MessagePageViewModel(serviceManager, contacts);
        if (topicName is null)
        {
            await vm.InitializeAsync();
        }
        else
        {
            vm.TopicsList.Add(topicName);
            Dispatcher.UIThread.Post(() =>
                vm.SendMessageTopicName = topicName,
                DispatcherPriority.Input
                );
        }
        CurrentPage = vm;
    }

    public void NavigateTorrentView()
    {
        CurrentPage = torrentPage;
    }

    public void NavigateConfigView()
    {
        CurrentPage = configPage;
    }

    public void NavigatePurchaseView()
    {
        CurrentPage = purchasePage;
    }

    public void RegisterTopLevel(TopLevel topLevel)
    {
        serviceManager.GetOrRegister(topLevel);
    }
}
