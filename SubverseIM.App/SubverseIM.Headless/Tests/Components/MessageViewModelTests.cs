using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Components;

public class MessageViewModelTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public MessageViewModelTests(MainViewFixture fixture)
    {
        this.fixture = fixture;
    }

    private async Task<MainView> EnsureMainViewLoaded()
    {
        fixture.EnsureWindowShown();

        MainView mainView = fixture.GetView();
        await mainView.LoadTask;

        return mainView;
    }

    private async Task<(MessagePageView, MessagePageViewModel)> EnsureIsOnMessagePageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        IServiceManager serviceManager = fixture.GetServiceManager();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        mainViewModel.NavigateMessageView(dbService.GetContacts(), null);

        MessagePageViewModel? messagePageViewModel = mainViewModel.CurrentPage as MessagePageViewModel;
        Assert.NotNull(messagePageViewModel);

        MessagePageView? messagePageView = mainView.GetContentAs<MessagePageView>();
        Assert.NotNull(messagePageView);

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

        MessageViewModel? messageViewModel = messagePageViewModel.MessageList.FirstOrDefault(x => x.Content == checkToken);
        Debug.Assert(messageViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await messageViewModel.DeleteCommand();

        Assert.DoesNotContain(messageViewModel, messagePageViewModel.MessageList);
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

        MessageViewModel? messageViewModel = messagePageViewModel.MessageList.FirstOrDefault(x => x.Content == checkToken);
        Debug.Assert(messageViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await messageViewModel.DeleteCommand();

        IServiceManager serviceManager = fixture.GetServiceManager();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        IEnumerable<SubverseContact> contacts = dbService.GetContacts();
        IEnumerable<SubverseMessage> messages = dbService.GetMessagesWithPeersOnTopic(
            contacts.Select(x => x.OtherPeer).ToHashSet(), MainViewFixture.EXPECTED_TOPIC_NAME
            );
        Assert.DoesNotContain(checkToken, messages.Select(x => x.Content));
    }
}
