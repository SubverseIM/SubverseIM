using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class ContactPageViewTests
{
    private readonly MainViewFixture fixture;

    public ContactPageViewTests()
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

    private async Task<(ContactPageView, ContactPageViewModel)> EnsureIsOnContactPageView(MessagePageViewModel? parentOrNull = null)
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        while (await navService.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        await navService.NavigateContactViewAsync(parentOrNull);

        ContactPageView? contactPageView = mainView.GetContentAs<ContactPageView>();
        Assert.NotNull(contactPageView);

        ContactPageViewModel? contactPageViewModel = contactPageView.DataContext as ContactPageViewModel;
        Assert.NotNull(contactPageViewModel);

        if (contactPageView.LoadTask.IsCompleted)
        {
            await contactPageViewModel.LoadContactsAsync();
        }
        else
        {
            await contactPageView.LoadTask;
        }

        return (contactPageView, contactPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldNotSetParentIfNull()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        Assert.Null(contactPageViewModel.Parent);
    }

    [AvaloniaFact]
    public async Task ShouldSetParentIfNotNull()
    {
        await fixture.InitializeAsync();

        MessagePageViewModel messagePageViewModel = new(await fixture.GetServiceManagerAsync(), []);
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView(messagePageViewModel);

        Assert.NotNull(contactPageViewModel.Parent);
    }

    [AvaloniaFact]
    public async Task ShouldOpenSettingsView()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        await contactPageViewModel.OpenSettingsCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<ConfigPageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldOpenFilesView()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        await contactPageViewModel.OpenFilesCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<TorrentPageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldOpenProductsView()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        await contactPageViewModel.OpenProductsCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<PurchasePageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldLoadContacts()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        Assert.Equal(
            MainViewFixture.EXPECTED_NUM_CONTACTS,
            contactPageViewModel.ContactsList.Count(x => x.TopicName is null)
            );
    }

    [AvaloniaFact]
    public async Task ShouldLoadTopics()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        Assert.Contains(
            MainViewFixture.EXPECTED_TOPIC_NAME,
            contactPageViewModel.ContactsList
            .Select(x => x.TopicName)
            );
    }

    [AvaloniaFact]
    public async Task ShouldOpenConversationViewWithContactsSelected()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        foreach (ContactViewModel contactViewModel in contactPageViewModel.ContactsList)
        {
            contactViewModel.IsSelected = contactViewModel.TopicName is null;
        }
        await contactPageViewModel.MessageCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<MessagePageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldOpenConversationViewWithOneTopicSelected()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        foreach (ContactViewModel contactViewModel in contactPageViewModel.ContactsList)
        {
            contactViewModel.IsSelected = contactViewModel.TopicName is not null;
        }
        await contactPageViewModel.MessageCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<MessagePageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldNotOpenConversationViewWithMultipleTopicsSelected()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        foreach (ContactViewModel contactViewModel in contactPageViewModel.ContactsList)
        {
            contactViewModel.IsSelected = true;
        }
        await contactPageViewModel.MessageCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<ContactPageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldNotOpenConversationViewWithoutContactsSelected()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        foreach (ContactViewModel contactViewModel in contactPageViewModel.ContactsList)
        {
            contactViewModel.IsSelected = false;
        }
        await contactPageViewModel.MessageCommand();

        Page? currentPageView = (await fixture.GetViewAsync()).CurrentPage;
        Assert.IsType<ContactPageView>(currentPageView);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveContact()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        ContactViewModel contactViewModel = new(
            await fixture.GetServiceManagerAsync(),
            contactPageViewModel,
            new Models.SubverseContact()
            );
        contactPageViewModel.ContactsList.Add(contactViewModel);

        contactPageViewModel.RemoveContact(contactViewModel);

        Assert.DoesNotContain(contactViewModel, contactPageViewModel.ContactsList);
    }

    [AvaloniaFact]
    public async Task ShouldAddParticipantsWithParent()
    {
        await fixture.InitializeAsync();
        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();

        MessagePageViewModel messagePageViewModel = new(serviceManager, []);
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView(messagePageViewModel);

        foreach (ContactViewModel contactViewModel in contactPageViewModel.ContactsList.Where(x => x.TopicName is null))
        {
            contactViewModel.IsSelected = true;
        }
        await contactPageViewModel.AddParticipantsCommand();

        messagePageViewModel.ShouldRefreshContacts = true;
        messagePageViewModel.SendMessageTopicName = MainViewFixture.EXPECTED_TOPIC_NAME;
        await messagePageViewModel.InitializeAsync();

        Assert.Equal(
            contactPageViewModel.ContactsList.Count(x => x.TopicName is null),
            messagePageViewModel.ContactsList.Count
            );
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        INavigationService navService = await serviceManager.GetWithAwaitAsync<INavigationService>();
        Exception? exception = null;
        try
        {
            await navService.NavigatePreviousViewAsync(shouldForceNavigation: true);
        }
        catch (Exception ex) { exception = ex; }
        Assert.Null(exception);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousViewWithParent()
    {
        await fixture.InitializeAsync();
        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();

        MessagePageViewModel messagePageViewModel = new(serviceManager, []);
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView(messagePageViewModel);

        INavigationService navService = await serviceManager.GetWithAwaitAsync<INavigationService>();
        Exception? exception = null;
        try
        {
            await navService.NavigatePreviousViewAsync(shouldForceNavigation: true);
        }
        catch (Exception ex) { exception = ex; }
        Assert.Null(exception);
    }
}
