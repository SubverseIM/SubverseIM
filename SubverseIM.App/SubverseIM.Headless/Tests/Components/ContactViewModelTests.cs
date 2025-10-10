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

public class ContactViewModelTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public ContactViewModelTests(MainViewFixture fixture)
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

    private async Task<(CreateContactPageView, CreateContactPageViewModel)> EnsureIsOnCreateContactPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        IServiceManager serviceManager = fixture.GetServiceManager();
        IDbService? dbService = serviceManager.Get<IDbService>();
        Assert.NotNull(dbService);

        SubverseContact? contact = (await dbService.GetContactsAsync()).FirstOrDefault();
        Assert.NotNull(contact);

        mainViewModel.NavigateContactView(contact);

        CreateContactPageViewModel? createContactPageViewModel = mainViewModel.CurrentPage as CreateContactPageViewModel;
        Assert.NotNull(createContactPageViewModel);

        CreateContactPageView? createContactPageView = mainView.GetContentAs<CreateContactPageView>();
        Assert.NotNull(createContactPageView);

        return (createContactPageView, createContactPageViewModel);
    }

    private async Task<(MessagePageView, MessagePageViewModel)> EnsureIsOnMessagePageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

        IServiceManager serviceManager = fixture.GetServiceManager();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        mainViewModel.NavigateMessageView(await dbService.GetContactsAsync(), null);

        MessagePageViewModel? messagePageViewModel = mainViewModel.CurrentPage as MessagePageViewModel;
        Assert.NotNull(messagePageViewModel);

        MessagePageView? messagePageView = mainView.GetContentAs<MessagePageView>();
        Assert.NotNull(messagePageView);

        await messagePageView.LoadTask;

        return (messagePageView, messagePageViewModel);
    }

    private ContactViewModel? GetContactViewModel<T>(T container)
        where T : PageViewModelBase<T>
    {
        if (container is ContactPageViewModel contactPageViewModel)
        {
            return contactPageViewModel.ContactsList.FirstOrDefault();
        }
        else if (container is CreateContactPageViewModel createContactPageViewModel) 
        {
            return createContactPageViewModel.Contact;
        }
        else if (container is MessagePageViewModel messagePageViewModel)
        {
            return messagePageViewModel.ContactsList.FirstOrDefault();
        }
        else
        {
            return null;
        }
    }

    [AvaloniaFact]
    public async Task ShouldInitializePhotoInContactsView() 
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(contactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        Assert.NotNull(contactViewModel.ContactPhoto);
    }

    [AvaloniaFact]
    public async Task ShouldInitializePhotoInCreateContactView()
    {
        (CreateContactPageView createContactPageView,
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(createContactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        Assert.NotNull(contactViewModel.ContactPhoto);
    }

    [AvaloniaFact]
    public async Task ShouldNotInitializePhotoInConversationView() 
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        ContactViewModel? contactViewModel = GetContactViewModel(messagePageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        Assert.Null(contactViewModel.ContactPhoto);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveSelfInContactsView() 
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(contactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.DeleteCommand(deleteFromDb: false);

        Assert.DoesNotContain(contactViewModel, contactPageViewModel.ContactsList);
    }

    [AvaloniaFact]
    public async Task ShouldRemoveSelfInConversationView()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        ContactViewModel? contactViewModel = GetContactViewModel(messagePageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.DeleteCommand(deleteFromDb: false);

        Assert.DoesNotContain(contactViewModel, messagePageViewModel.ContactsList);
    }

    [AvaloniaFact]
    public async Task ShouldNotRemoveSelfInEditContactView() 
    {
        (CreateContactPageView createContactPageView,
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(createContactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.DeleteCommand(deleteFromDb: false);

        Assert.NotNull(createContactPageViewModel.Contact);
    }

    [AvaloniaFact]
    public async Task ShouldReturnToPreviousViewOnEditCancel()
    {
        (CreateContactPageView createContactPageView,
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(createContactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.CancelCommand();

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsNotType<CreateContactPageViewModel>(currentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldReturnToPreviousViewOnEditSaved()
    {
        (CreateContactPageView createContactPageView,
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(createContactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.SaveChangesCommand();

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsNotType<CreateContactPageViewModel>(currentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldPersistChangesOnEditSaved()
    {
        (CreateContactPageView createContactPageView,
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(createContactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        string checkToken = RandomNumberGenerator.GetHexString(5);
        contactViewModel.UserNote = checkToken;
        await contactViewModel.SaveChangesCommand();

        IServiceManager serviceManager = fixture.GetServiceManager();
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
        Assert.Contains(checkToken, (await dbService.GetContactsAsync()).Select(x => x.UserNote));

        contactViewModel.UserNote = null;
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToCreateContactPageOnEdit_FromContactsView()
    {
        (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
            await EnsureIsOnContactPageView();

        ContactViewModel? contactViewModel = GetContactViewModel(contactPageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.EditCommand();

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsType<CreateContactPageViewModel>(currentPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToCreateContactPageOnEdit_FromConversationView()
    {
        (MessagePageView messagePageView, MessagePageViewModel messagePageViewModel) =
            await EnsureIsOnMessagePageView();

        ContactViewModel? contactViewModel = GetContactViewModel(messagePageViewModel);
        Debug.Assert(contactViewModel is not null); // should always be non-null, test should be rewritten otherwise.

        await contactViewModel.EditCommand();

        PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
        Assert.IsType<CreateContactPageViewModel>(currentPageViewModel);
    }
}
