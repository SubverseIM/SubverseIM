using LiteDB;
using Mono.Nat;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase, IFrontendService, IDisposable
{
    private readonly IServiceManager serviceManager;

    private readonly ContactPageViewModel contactPage;

    private readonly CreateContactPageViewModel createContactPage;

    private readonly Dictionary<SubversePeerId, MessagePageViewModel> messagePageMap;

    private readonly CancellationTokenSource mainTaskCts;

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

        messagePageMap = new();

        currentPage = contactPage;

        mainTaskCts = new();
        _ = RunAsync(mainTaskCts.Token);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        IPeerService peerService = await serviceManager.GetWithAwaitAsync<IPeerService>();
        INativeService nativeService = await serviceManager.GetWithAwaitAsync<INativeService>();

        _ = peerService.BootstrapSelfAsync(cancellationToken);

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
                    DisplayName = message.Sender.ToString(), 
                    UserNote = "Anonymous User" 
                };

            dbService.InsertOrUpdateItem(contact);
            await contactPage.LoadContactsAsync(cancellationToken);

            try
            {
                dbService.InsertOrUpdateItem(message);
                await nativeService.SendPushNotificationAsync(
                        message.Sender.GetHashCode(), contact?.DisplayName ?? "Anonymous",
                        message.Content ?? "Message did not contain text."
                        );

                if (contact is not null && messagePageMap.TryGetValue(contact.OtherPeer, out MessagePageViewModel? vm))
                {
                    vm.MessageList.Insert(0, new(contact, message));
                }
            }
            catch (LiteException ex) when (ex.ErrorCode == LiteException.INDEX_DUPLICATE_KEY) { }
        }
    }

    public async Task InvokeFromLauncherAsync()
    {
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
        Uri? launchedUri = launcherService.GetLaunchedUri();
        if (launchedUri is not null)
        {
            await createContactPage.InitializeAsync(launchedUri);
            CurrentPage = createContactPage;
        }
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

    public void NavigateMessageView(SubverseContact contact)
    {
        if (!messagePageMap.TryGetValue(contact.OtherPeer, out MessagePageViewModel? vm))
        {
            messagePageMap.Add(contact.OtherPeer, vm = new(serviceManager, contact));
        }
        CurrentPage = vm;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                mainTaskCts.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
