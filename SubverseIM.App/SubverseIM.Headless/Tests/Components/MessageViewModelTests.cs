using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SubverseIM.Headless.Tests.Components;

public class MessageViewModelTests
{
    private readonly MainViewFixture fixture;

    public MessageViewModelTests()
    {
        fixture = new MainViewFixture();
    }

    private async Task<MainView> EnsureMainViewLoaded()
    {
        await fixture.InitializeAsync();

        MainView mainView = await fixture.GetViewAsync();
        await mainView.LoadTask;

        return mainView;
    }

    private async Task<(MessagePageView, MessagePageViewModel)> EnsureIsOnMessagePageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        while (await navService.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        await navService.NavigateMessageViewAsync(await dbService.GetContactsAsync(), null);

        MessagePageView? messagePageView = mainView.GetContentAs<MessagePageView>();
        Assert.NotNull(messagePageView);

        MessagePageViewModel? messagePageViewModel = messagePageView.DataContext as MessagePageViewModel;
        Assert.NotNull(messagePageViewModel);

        await messagePageView.LoadTask;

        return (messagePageView, messagePageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveFromConversationViewOnDelete()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        string checkToken = RandomNumberGenerator.GetHexString(5);
        messagePageViewModel.SendMessageTopicName = MainViewFixture.EXPECTED_TOPIC_NAME;
        messagePageViewModel.SendMessageText = checkToken;
        await messagePageViewModel.SendCommand();

        MessageViewModel? messageViewModel = messagePageViewModel.SortedMessageList.FirstOrDefault(x => x.ContentString == checkToken);
        Debug.Assert(messageViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await messageViewModel.DeleteCommand();

        Assert.DoesNotContain(messageViewModel, messagePageViewModel.SortedMessageList);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveFromDatabaseOnDelete()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        string checkToken = RandomNumberGenerator.GetHexString(5);
        messagePageViewModel.SendMessageTopicName = MainViewFixture.EXPECTED_TOPIC_NAME;
        messagePageViewModel.SendMessageText = checkToken;
        await messagePageViewModel.SendCommand();

        MessageViewModel? messageViewModel = messagePageViewModel.SortedMessageList.FirstOrDefault(x => x.ContentString == checkToken);
        Debug.Assert(messageViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await messageViewModel.DeleteCommand();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        IEnumerable<SubverseContact> contacts = await dbService.GetContactsAsync();
        IEnumerable<SubverseMessage> messages = await dbService.GetMessagesWithPeersOnTopicAsync(
            contacts.Select(x => x.OtherPeer).ToHashSet(), MainViewFixture.EXPECTED_TOPIC_NAME
            );
        Assert.DoesNotContain(checkToken, messages.Select(x => x.Content));
    }
}
