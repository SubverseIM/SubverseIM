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
            dbService.InsertOrUpdateItem(message);
        }
    }

    public async Task ViewCreateContactAsync(Uri contactUri, CancellationToken cancellationToken)
    {
        await createContactPage.InitializeAsync(contactUri, cancellationToken);
        CurrentPage = createContactPage;
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
