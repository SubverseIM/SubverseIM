using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class ContactPageViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public ContactPageViewTests(MainViewFixture fixture)
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

    private async Task<(ContactPageView, ContactPageViewModel)> EnsureIsOnContactPageView(MessagePageViewModel? parentOrNull = null)
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        mainViewModel.NavigateContactView(parentOrNull);

        ContactPageViewModel? contactPageViewModel = mainViewModel.CurrentPage as ContactPageViewModel;
        Assert.NotNull(contactPageViewModel);

        ContactPageView? contactPageView = mainView.GetContentAs<ContactPageView>();
        Assert.NotNull(contactPageView);

        await contactPageView.LoadTask;

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
        MessagePageViewModel messagePageViewModel = new(fixture.GetServiceManager(), []);
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

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsType<ConfigPageViewModel>(currentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldOpenFilesView()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        await contactPageViewModel.OpenFilesCommand();

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsType<TorrentPageViewModel>(currentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldLoadContacts()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        Assert.Equal(
            MainViewFixture.EXPECTED_NUM_CONTACTS,
            contactPageViewModel.ContactsList.Count
            );
    }

    [AvaloniaFact]
    public async Task ShouldLoadTopics()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        Assert.Contains(
            MainViewFixture.EXPECTED_TOPIC_NAME,
            contactPageViewModel.TopicsList
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
            contactViewModel.IsSelected = true;
        }
        await contactPageViewModel.MessageCommand();

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsType<MessagePageViewModel>(currentPageViewModel);
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

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsType<ContactPageViewModel>(currentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveContact()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        ContactViewModel contactViewModel = new(
            fixture.GetServiceManager(),
            contactPageViewModel,
            new Models.SubverseContact()
            );
        contactPageViewModel.ContactsList.Add(contactViewModel);

        contactPageViewModel.RemoveContact(contactViewModel);

        Assert.DoesNotContain(contactViewModel, contactPageViewModel.ContactsList);
    }
}
