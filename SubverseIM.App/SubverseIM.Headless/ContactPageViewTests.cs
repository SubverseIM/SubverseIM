using Avalonia.Headless.XUnit;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless
{
    public class ContactPageViewTests : IClassFixture<MainViewFixture>
    {
        private readonly MainViewFixture fixture;

        public ContactPageViewTests(MainViewFixture fixture)
        {
            this.fixture = fixture;
        }

        /* TODO: Write tests for topics list */

        private async Task<(ContactPageView, ContactPageViewModel)> EnsureIsOnContactPageView(MessagePageViewModel? parentOrNull = null)
        {
            fixture.EnsureWindowShown();

            MainView mainView = fixture.GetView();
            await mainView.LoadTask;

            MainViewModel mainViewModel = fixture.GetViewModel();
            while (mainViewModel.HasPreviousView && mainViewModel.NavigatePreviousView()) ;

            mainViewModel.NavigateContactView(parentOrNull);

            ContactPageView? contactPageView = mainView.GetContentAs<ContactPageView>();
            Assert.NotNull(contactPageView);

            ContactPageViewModel? contactPageViewModel = mainViewModel.CurrentPage as ContactPageViewModel;
            Assert.NotNull(contactPageViewModel);

            return (contactPageView, contactPageViewModel);
        }

        [AvaloniaFact]
        public async Task ShouldNotBeDialogWithoutParent()
        {
            (ContactPageView _, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView();

            Assert.False(contactPageViewModel.IsDialog);
        }

        [AvaloniaFact]
        public async Task ShouldBeDialogWithParent()
        {
            MessagePageViewModel messagePageViewModel = new(fixture.GetServiceManager(), []);
            (ContactPageView _, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView(messagePageViewModel);

            Assert.True(contactPageViewModel.IsDialog);
        }

        [AvaloniaFact]
        public async Task ShouldLoadContacts()
        {
            (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView();
            await contactPageView.LoadTask;

            Assert.Equal(
                MainViewFixture.EXPECTED_NUM_CONTACTS, 
                contactPageViewModel.ContactsList.Count
                );
        }

        [AvaloniaFact]
        public async Task ShouldOpenConversationViewWithContactsSelected()
        {
            (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView();
            await contactPageView.LoadTask;

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
            await contactPageView.LoadTask;

            foreach (ContactViewModel contactViewModel in contactPageViewModel.ContactsList)
            {
                contactViewModel.IsSelected = false;
            }
            await contactPageViewModel.MessageCommand();

            PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
            Assert.IsNotType<MessagePageViewModel>(currentPageViewModel);
        }

        [AvaloniaFact]
        public async Task ShouldRemoveContact()
        {
            (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView();

            await contactPageView.LoadTask;

            ContactViewModel contactViewModel = new(
                fixture.GetServiceManager(), 
                contactPageViewModel, 
                new Models.SubverseContact()
                );
            contactPageViewModel.ContactsList.Add(contactViewModel);

            contactPageViewModel.RemoveContact(contactViewModel);
            PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;

            Assert.IsNotType<MessagePageViewModel>(currentPageViewModel);
        }

        [AvaloniaFact]
        public async Task ShouldOpenSettingsView()
        {
            (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView();
            await contactPageView.LoadTask;

            await contactPageViewModel.OpenSettingsCommand();

            PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
            Assert.IsType<ConfigPageView>(currentPageViewModel);
        }

        [AvaloniaFact]
        public async Task ShouldOpenFilesView()
        {
            (ContactPageView contactPageView, ContactPageViewModel contactPageViewModel) =
                await EnsureIsOnContactPageView();
            await contactPageView.LoadTask;

            await contactPageViewModel.OpenFilesCommand();

            PageViewModelBase currentPageViewModel = fixture.GetViewModel().CurrentPage;
            Assert.IsType<TorrentPageView>(currentPageViewModel);
        }
    }
}
