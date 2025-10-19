using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class CreateContactPageViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public CreateContactPageViewTests(MainViewFixture fixture)
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

    private async Task<(CreateContactPageView, CreateContactPageViewModel)> EnsureIsOnCreateContactPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = fixture.GetViewModel();
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

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

    [AvaloniaFact]
    public async Task ShouldInitializeContactViewModel() 
    {
        (CreateContactPageView createContactPageView, 
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        Assert.NotNull(createContactPageViewModel.Contact);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView()
    {
        (CreateContactPageView createContactPageView,
            CreateContactPageViewModel createContactPageViewModel) =
            await EnsureIsOnCreateContactPageView();

        IServiceManager serviceManager = fixture.GetServiceManager();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        await frontendService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainViewModel mainViewModel = fixture.GetViewModel();
        Assert.IsNotType<CreateContactPageViewModel>(mainViewModel.CurrentPage);
    }
}
