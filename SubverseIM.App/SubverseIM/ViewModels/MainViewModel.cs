using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using LiteDB;
using MonoTorrent;
using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

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

            PageViewModelBase previousPage = currentPage;
            this.RaiseAndSetIfChanged(ref currentPage, value);

            currentPage.UseThemeOverride = previousPage.UseThemeOverride;
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
        SubverseContact? thisContact = await dbService.GetContactAsync(thisPeer);

        List<Task> subTasks =
        [
            Task.Run(Task? () => messageService.ProcessRelayAsync(cancellationToken)),
            Task.Run(Task? () => messageService.ResendAllUndeliveredMessagesAsync(cancellationToken)),
            Task.Run(Task? () => bootstrapperService.BootstrapSelfAsync(cancellationToken)),
            Task.Run(torrentPage.InitializeAsync),
        ];

        IEnumerable<SubverseContact> contacts = await dbService.GetContactsAsync(cancellationToken);
        lock (messageService.CachedPeers)
        {
            foreach (SubverseContact contact in contacts.Where(x => x.TopicName is null))
            {
                messageService.CachedPeers.TryAdd(
                    contact.OtherPeer,
                    new SubversePeer
                    {
                        OtherPeer = contact.OtherPeer
                    });
            }
        }

        subTasks.Add(Task.Run(async Task? () =>
        {
            SubverseMessage joinMessage = new SubverseMessage()
            {
                TopicName = "#system",
                MessageId = new(CallProperties.CreateNewCallId(), thisPeer),

                Sender = thisPeer,
                SenderName = thisContact?.DisplayName ?? "Anonymous",

                Recipients = (await dbService.GetContactsAsync(cancellationToken))
                    .Where(x => x.TopicName is null)
                    .Select(x => x.OtherPeer)
                    .ToArray(),
                RecipientNames = (await dbService.GetContactsAsync(cancellationToken))
                    .Where(x => x.TopicName is null)
                    .Select(x => x.DisplayName ?? "Anonymous")
                    .ToArray(),

                Content = "<joined SubverseIM>",
                DateSignedOn = DateTime.UtcNow,
            };

            await messageService.SendMessageAsync(joinMessage, cancellationToken: cancellationToken);
        }));

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

                SubverseContact? contact = await dbService.GetContactAsync(topicId ?? message.Sender, cancellationToken);
                if (contact is not null)
                {
                    contact.DateLastChattedWith = message.DateSignedOn;
                    await dbService.InsertOrUpdateItemAsync(contact, cancellationToken);

                    lock (messageService.CachedPeers)
                    {
                        messageService.CachedPeers.TryAdd(
                            contact.OtherPeer,
                            new SubversePeer
                            {
                                OtherPeer = contact.OtherPeer
                            });
                    }
                }

                try
                {
                    await dbService.InsertOrUpdateItemAsync(message, cancellationToken);
                    await contactPage.LoadContactsAsync(cancellationToken);

                    bool isCurrentPeer = false;
                    if (contact is not null && currentPage is MessagePageViewModel vm &&
                        (isCurrentPeer = vm.ContactsList.Any(x => x.innerContact.OtherPeer == message.Sender) &&
                        message.TopicName != "#system" && (message.WasDecrypted ?? true) && (message.TopicName == vm.SendMessageTopicName ||
                        (string.IsNullOrEmpty(message.TopicName) && string.IsNullOrEmpty(vm.SendMessageTopicName))
                        )))
                    {
                        vm.MessageList.Add(message);

                        if (!string.IsNullOrEmpty(message.TopicName) &&
                            !vm.TopicsList.Contains(message.TopicName))
                        {
                            vm.TopicsList.Insert(0, message.TopicName);
                        }

                        foreach (SubverseContact participant in message.Recipients
                            .Zip(message.RecipientNames)
                            .Select(x => new SubverseContact()
                            {
                                OtherPeer = x.First,
                                DisplayName = x.Second,
                            }))
                        {
                            if (participant.OtherPeer == thisPeer) continue;
                            vm.AddUniqueParticipant(participant, false);
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
                await NavigatePurchaseViewAsync();
            }
        }
    }

    public async Task<bool> NavigatePreviousViewAsync(bool shouldForceNavigation)
    {
        bool confirm;
        if (!shouldForceNavigation && currentPage.ShouldConfirmBackNavigation)
        {
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            confirm = await launcherService.ShowConfirmationDialogAsync(
                "Confirm Navigation",
                "Are you sure you want to go back? Unsaved changes may be lost."
                );
        }
        else
        {
            confirm = true;
        }

        if (confirm && previousPages.TryPop(out PageViewModelBase? previousPage))
        {
            previousPage.UseThemeOverride = currentPage.UseThemeOverride;

            this.RaiseAndSetIfChanged(ref currentPage, previousPage, nameof(CurrentPage));
            HasPreviousView = previousPages.Count > 0;
            return true;
        }
        else
        {
            return false;
        }
    }

    public async Task NavigateLaunchedUriAsync(Uri? overrideUri = null)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
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
                if (MagnetLink.TryParse(launchedUri.OriginalString, out MagnetLink? magnetLink))
                {
                    SubverseTorrent torrent = new SubverseTorrent(
                        magnetLink.InfoHashes.V1OrV2,
                        launchedUri.OriginalString
                        );
                    await dbService.InsertOrUpdateItemAsync(torrent);

                    await torrentService.AddTorrentAsync(magnetLink.InfoHashes.V1OrV2);
                    await torrentService.StartAsync(torrent);

                    CurrentPage = torrentPage;
                }
                break;
            case null:
                break;
        }

        // Synchronize purchases
        await RestorePurchasesAsync();
        await PromptForPurchaseAsync();
    }

    public Task NavigateContactViewAsync(MessagePageViewModel? parentOrNull)
    {
        contactPage.Parent = parentOrNull;
        contactPage.IsSidebarOpen = false;

        CurrentPage = contactPage;
        return Task.CompletedTask;
    }

    public async Task NavigateContactViewAsync(SubverseContact contact)
    {
        if (CurrentPage is MessagePageViewModel messagePageViewModel)
        {
            messagePageViewModel.ShouldRefreshContacts = false;
        }

        await createContactPage.InitializeAsync(new Uri($"sv://{contact.OtherPeer}?name={HttpUtility.UrlEncode(contact.DisplayName)}"));
        CurrentPage = createContactPage;
    }

    public async Task NavigateMessageViewAsync(IEnumerable<SubverseContact> contacts, string? topicName)
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

    public Task NavigateTorrentViewAsync()
    {
        CurrentPage = torrentPage;
        return Task.CompletedTask;
    }

    public Task NavigateConfigViewAsync()
    {
        CurrentPage = configPage;
        return Task.CompletedTask;
    }

    public Task NavigatePurchaseViewAsync()
    {
        CurrentPage = purchasePage;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Uri>> ShowUploadDialogAsync(string sourceFilePath)
    {
        CurrentPage = new UploadPageViewModel(serviceManager, sourceFilePath);

        IReadOnlyList<Uri> resultUris = await ((UploadPageViewModel)CurrentPage).GetUriListAsync();
        await NavigatePreviousViewAsync(shouldForceNavigation: true);

        return resultUris;
    }

    public void RegisterTopLevel(TopLevel topLevel)
    {
        serviceManager.GetOrRegister(topLevel);
    }
}
