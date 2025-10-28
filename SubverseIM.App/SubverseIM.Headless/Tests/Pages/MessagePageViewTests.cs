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

namespace SubverseIM.Headless.Tests.Pages;
public class MessagePageViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public MessagePageViewTests(MainViewFixture fixture)
    {
        this.fixture = fixture;
    }

    private async Task<MainView> EnsureMainViewLoaded()
    {
        await fixture.InitializeOnceAsync();

        MainView mainView = await fixture.GetViewAsync();
        await mainView.LoadTask;

        return mainView;
    }

    private async Task<(MessagePageView, MessagePageViewModel)> EnsureIsOnMessagePageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        await mainViewModel.NavigateMessageViewAsync((await dbService.GetContactsAsync()).Where(x => x.TopicName is null), null);

        MessagePageViewModel? messagePageViewModel = mainViewModel.CurrentPage as MessagePageViewModel;
        Assert.NotNull(messagePageViewModel);

        MessagePageView? messagePageView = mainView.GetContentAs<MessagePageView>();
        Assert.NotNull(messagePageView);

        await messagePageView.LoadTask;

        return (messagePageView, messagePageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldLoadParticipants()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        Assert.Equal(
            MainViewFixture.EXPECTED_NUM_CONTACTS,
            messagePageViewModel.ContactsList.Count
            );
    }

    [AvaloniaFact]
    public async Task ShouldAddTopicWithValidation()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        await messagePageViewModel.AddTopicCommand("Hello world!");

        Assert.Contains("#hello-world", messagePageViewModel.TopicsList);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToContactsViewOnAddParticipant()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        await messagePageViewModel.AddParticipantsCommand();

        PageViewModelBase currentPageViewModel = (await fixture.GetViewModelAsync()).CurrentPage;
        Assert.IsType<ContactPageViewModel>(currentPageViewModel);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAddParticipantIfUnique(bool permanent)
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        messagePageViewModel.SendMessageTopicName = null;

        string checkToken = RandomNumberGenerator.GetHexString(5);
        SubverseContact contact = new SubverseContact
        {
            OtherPeer = new(RandomNumberGenerator.GetBytes(20)),
            DisplayName = "Anonymous",
            UserNote = checkToken
        };
        messagePageViewModel.AddUniqueParticipant(contact, permanent);

        messagePageViewModel.SendMessageTopicName = MainViewFixture.EXPECTED_TOPIC_NAME;

        Assert.Equal(messagePageViewModel.ContactsList
            .Select(x => x.UserNote)
            .Contains(checkToken), !permanent);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNotAddParticipantIfRedundant(bool permanent)
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        messagePageViewModel.SendMessageTopicName = null;

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

        SubverseContact? contact = (await dbService.GetContactsAsync()).FirstOrDefault(x => x.TopicName is null);
        Debug.Assert(contact is not null); // should always be non-null, test should be rewritten otherwise.

        int countBefore = messagePageViewModel.ContactsList.Count(x => x.TopicName is null);
        messagePageViewModel.AddUniqueParticipant(contact, permanent);
        int countAfter = messagePageViewModel.ContactsList.Count(x => x.TopicName is null);

        messagePageViewModel.SendMessageTopicName = MainViewFixture.EXPECTED_TOPIC_NAME;

        Assert.Equal(countBefore, countAfter);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveContactAsParticipant() 
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        ContactViewModel contactViewModel = new(
            await fixture.GetServiceManagerAsync(),
            messagePageViewModel,
            new SubverseContact()
            );
        messagePageViewModel.ContactsList.Add(contactViewModel);

        messagePageViewModel.RemoveContact(contactViewModel);

        Assert.DoesNotContain(contactViewModel, messagePageViewModel.ContactsList);
    }

    [AvaloniaFact]
    public async Task ShouldLoadMessagesFromSelectedTopic()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        messagePageViewModel.SendMessageTopicName = MainViewFixture.EXPECTED_TOPIC_NAME;
        await messagePageViewModel.InitializeAsync();

        Assert.NotEmpty(messagePageViewModel.MessageList);
    }

    [AvaloniaFact]
    public async Task ShouldClearMessageTextBoxOnSend()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        messagePageViewModel.SendMessageText = "Hello world!";
        await messagePageViewModel.SendCommand();

        bool result = string.IsNullOrEmpty(messagePageViewModel.SendMessageText);
        Assert.True(result);
    }

    [AvaloniaFact]
    public async Task ShouldAddMessageToListOnSend()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        string checkToken = RandomNumberGenerator.GetHexString(5);
        messagePageViewModel.SendMessageText = checkToken;
        await messagePageViewModel.SendCommand();

        Assert.Contains(checkToken, messagePageViewModel.MessageList.Select(x => x.ContentString));
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView() 
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        await frontendService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        Assert.IsNotType<MessagePageViewModel>(mainViewModel.CurrentPage);
    }
}