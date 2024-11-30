using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LiteDB;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
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

    private PageViewModelBase currentPage;

    private bool disposedValue;

    public PageViewModelBase CurrentPage
    {
        get { return currentPage; }
        private set { this.RaiseAndSetIfChanged(ref currentPage, value); }
    }

    public MainViewModel(IServiceManager serviceManager)
    {
        this.serviceManager = serviceManager;
        serviceManager.GetOrRegister<IFrontendService>(this);

        contactPage = new(serviceManager);
        createContactPage = new(serviceManager);

        currentPage = contactPage;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
        IPeerService peerService = await serviceManager.GetWithAwaitAsync<IPeerService>(cancellationToken);
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);
        INativeService nativeService = await serviceManager.GetWithAwaitAsync<INativeService>(cancellationToken);

        _ = peerService.BootstrapSelfAsync(cancellationToken);

        _ = Task.Run(async Task? () =>
        {
            foreach (SubverseMessage message in dbService.GetAllUndeliveredMessages())
            {
                await peerService.SendMessageAsync(message, cancellationToken);
            }
        });

        lock (peerService.CachedPeers)
        {
            foreach (SubverseContact contact in dbService.GetContacts())
            {
                peerService.CachedPeers.Add(
                    contact.OtherPeer,
                    new SubversePeer
                    {
                        OtherPeer = contact.OtherPeer
                    });
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SubverseMessage message = await peerService.ReceiveMessageAsync(cancellationToken);
            SubverseContact contact = dbService.GetContact(message.Sender) ??
                new SubverseContact()
                {
                    OtherPeer = message.Sender,
                    DisplayName = "Anonymous",
                    UserNote = "Anonymous User via the Subverse Network"
                };

            contact.DateLastChattedWith = message.DateSignedOn;
            dbService.InsertOrUpdateItem(contact);

            await contactPage.LoadContactsAsync(cancellationToken);

            lock (peerService.CachedPeers)
            {
                peerService.CachedPeers.TryAdd(
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
                    (isCurrentPeer = vm.contacts.Any(x => x.OtherPeer == contact.OtherPeer) &&
                    (message.TopicName == vm.SendMessageTopicName ||
                    string.IsNullOrEmpty(vm.SendMessageTopicName))))
                {
                    if (!string.IsNullOrEmpty(message.TopicName) &&
                        !vm.TopicsList.Contains(message.TopicName))
                    {
                        vm.TopicsList.Insert(0, message.TopicName);
                    }

                    vm.MessageList.Insert(0, new(vm, contact, message));
                }

                if (launcherService.NotificationsAllowed && (!launcherService.IsInForeground || !isCurrentPeer))
                {
                    await nativeService.SendPushNotificationAsync(serviceManager, message, cancellationToken);
                }
                else
                {
                    nativeService.ClearNotification(message);
                }
            }
            catch (LiteException ex) when (ex.ErrorCode == LiteException.INDEX_DUPLICATE_KEY) { }
        }
    }

    public void RegisterStorageProvider(IStorageProvider storageProvider)
    {
        serviceManager.GetOrRegister(storageProvider);
    }

    public void NavigateContactView()
    {
        CurrentPage = contactPage;
    }

    public async void NavigateContactView(SubverseContact contact)
    {
        await createContactPage.InitializeAsync(new Uri($"sv://{contact.OtherPeer}"));
        CurrentPage = createContactPage;
    }

    public void NavigateMessageView(IEnumerable<SubverseContact> contacts)
    {
        CurrentPage = new MessagePageViewModel(serviceManager, contacts.ToArray());
    }

    public async void NavigateLaunchedUri()
    {
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
        Uri? launchedUri = launcherService.GetLaunchedUri();

        if (launchedUri is not null)
        {
            await createContactPage.InitializeAsync(launchedUri);
            CurrentPage = createContactPage;
        }
    }
}
