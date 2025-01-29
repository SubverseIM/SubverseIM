using Avalonia.Controls;
using LiteDB;
using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase, IFrontendService
{
    private readonly IServiceManager serviceManager;

    private readonly ContactPageViewModel contactPage;

    private readonly CreateContactPageViewModel createContactPage;

    private readonly TorrentPageViewModel torrentPage;

    private readonly ConfigPageViewModel configPage;

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
        configPage = new (serviceManager);

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
            return mainTask = RunAsync(cancellationToken);
        }
        else
        {
            return mainTask;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        IBootstrapperService bootstrapperService = await serviceManager.GetWithAwaitAsync<IBootstrapperService>();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);
        IMessageService messageService = await serviceManager.GetWithAwaitAsync<IMessageService>(cancellationToken);
        INativeService nativeService = await serviceManager.GetWithAwaitAsync<INativeService>(cancellationToken);

        SubversePeerId thisPeer = await bootstrapperService.GetPeerIdAsync(cancellationToken);
        SubverseContact? thisContact = dbService.GetContact(thisPeer);

        List<Task> subTasks =
        [
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
                    CallId = CallProperties.CreateNewCallId(),

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
                SubverseContact contact = dbService.GetContact(message.Sender) ??
                    new SubverseContact()
                    {
                        OtherPeer = message.Sender,
                        DisplayName = message.SenderName,
                    };

                contact.DateLastChattedWith = message.DateSignedOn;
                dbService.InsertOrUpdateItem(contact);

                await contactPage.LoadContactsAsync(cancellationToken);

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

                    bool isCurrentPeer = false;
                    if (contact is not null && currentPage is MessagePageViewModel vm &&
                        (isCurrentPeer = vm.ContactsList.Any(x => x.innerContact.OtherPeer == contact.OtherPeer) &&
                        message.TopicName != "#system" && (message.WasDecrypted ?? true) && (message.TopicName == vm.SendMessageTopicName ||
                        (string.IsNullOrEmpty(message.TopicName) && string.IsNullOrEmpty(vm.SendMessageTopicName))
                        )))
                    {
                        if (!string.IsNullOrEmpty(message.TopicName) &&
                            !vm.TopicsList.Contains(message.TopicName))
                        {
                            vm.TopicsList.Insert(0, message.TopicName);
                        }

                        MessageViewModel messageViewModel = new(vm, contact, message);
                        foreach (SubverseContact participant in messageViewModel.CcContacts)
                        {
                            if (participant.OtherPeer == thisPeer) continue;
                            vm.AddUniqueParticipant(participant, false);
                        }
                        vm.MessageList.Insert(0, messageViewModel);
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
        catch (OperationCanceledException) 
        {
            await torrentPage.DestroyAsync();
            await Task.WhenAll(subTasks);
            throw;
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
                await torrentService.AddTorrentAsync(launchedUri.ToString());
                await torrentPage.InitializeAsync();
                CurrentPage = torrentPage;
                break;
            case null:
                CurrentPage = contactPage;
                break;
        }
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

    public void NavigateMessageView(IEnumerable<SubverseContact> contacts, string? topicName)
    {
        MessagePageViewModel vm = new MessagePageViewModel(serviceManager, contacts);
        CurrentPage = vm;

        vm.SendMessageTopicName = topicName;
    }

    public async void NavigateTorrentView() 
    {
        await torrentPage.InitializeAsync();
        CurrentPage = torrentPage;
    }

    public async void NavigateConfigView()
    {
        await configPage.InitializeAsync();
        CurrentPage = configPage;
    }

    public void RegisterTopLevel(TopLevel topLevel)
    {
        serviceManager.GetOrRegister(topLevel);
    }
}
