using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using LiteDB;
using ReactiveUI;
using SIPSorcery.SIP;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase, IFrontendService
{
    private Task? mainTask;

    private Size? screenSize;
    public Size? ScreenSize
    {
        get => screenSize;
        set => this.RaiseAndSetIfChanged(ref screenSize, value);
    }

    public IServiceManager ServiceManager { get; }

    public MainViewModel(IServiceManager serviceManager)
    {
        ServiceManager = serviceManager;
        ServiceManager.GetOrRegister<IFrontendService>(this);
    }

    public async Task RunOnceBackgroundAsync()
    {
        if (mainTask?.IsCompleted ?? true)
        {
            INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>();
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
        IBootstrapperService bootstrapperService = await ServiceManager.GetWithAwaitAsync<IBootstrapperService>(cancellationToken);
        IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
        IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
        ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>(cancellationToken);
        IMessageService messageService = await ServiceManager.GetWithAwaitAsync<IMessageService>(cancellationToken);
        INativeService nativeService = await ServiceManager.GetWithAwaitAsync<INativeService>(cancellationToken);
        ITorrentService torrentService = await ServiceManager.GetWithAwaitAsync<ITorrentService>(cancellationToken);

        SubversePeerId thisPeer = await bootstrapperService.GetPeerIdAsync(cancellationToken);
        SubverseContact? thisContact = await dbService.GetContactAsync(thisPeer);

        List<Task> subTasks =
        [
            Task.Run(Task? () => messageService.ProcessRelayAsync(cancellationToken)),
            Task.Run(Task? () => messageService.ResendAllUndeliveredMessagesAsync(cancellationToken)),
            Task.Run(Task? () => bootstrapperService.BootstrapSelfAsync(cancellationToken)),
            Task.Run(Task? () => torrentService.InitializeAsync(cancellationToken))
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
            await messageService.RunAsync(cancellationToken);
        }
        finally
        {
            await torrentService.DestroyAsync(cancellationToken);
            await Task.WhenAll(subTasks);
        }
    }

    public async Task ResetSizeAsync()
    {
        TopLevel topLevel = await ServiceManager.GetWithAwaitAsync<TopLevel>();

        Screen? primaryScreen = topLevel.Screens?.Primary;
        if (primaryScreen is not null)
        {
            ScreenSize = primaryScreen.WorkingArea.Size.ToSize(primaryScreen.Scaling);
        }
    }

    public async Task RestorePurchasesAsync()
    {
        IBillingService billingService = await ServiceManager.GetWithAwaitAsync<IBillingService>();
        IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>();

        SubverseConfig config = await configurationService.GetConfigAsync();
        if (await billingService.WasAnyItemPurchasedAsync(["donation_small", "donation_normal", "donation_large"]))
        {
            config.DateLastPrompted = DateTime.UtcNow;
            config.PromptFreqIndex = null;

            await configurationService.PersistConfigAsync();
        }
    }
}
