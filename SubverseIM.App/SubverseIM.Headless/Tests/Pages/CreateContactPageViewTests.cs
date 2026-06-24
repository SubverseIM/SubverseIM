using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;

namespace SubverseIM.Headless.Tests.Pages;

public class CreateContactPageViewTests
{
    private readonly MainViewFixture fixture;

    public CreateContactPageViewTests()
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

    private async Task<(CreateContactPageView, CreateContactPageViewModel)> EnsureIsOnCreateContactPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        INavigationService navService = await mainViewModel.ServiceManager.GetWithAwaitAsync<INavigationService>();
        while (await navService.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IDbService? dbService = serviceManager.Get<IDbService>();
        Assert.NotNull(dbService);

        SubverseContact? contact = (await dbService.GetContactsAsync()).FirstOrDefault();
        Assert.NotNull(contact);

        await navService.NavigateContactViewAsync(contact);

        CreateContactPageView? createContactPageView = mainView.GetContentAs<CreateContactPageView>();
        Assert.NotNull(createContactPageView);

        CreateContactPageViewModel? createContactPageViewModel = createContactPageView.DataContext as CreateContactPageViewModel;
        Assert.NotNull(createContactPageViewModel);

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

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        INavigationService navService = await serviceManager.GetWithAwaitAsync<INavigationService>();
        await navService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainView mainView = await fixture.GetViewAsync();
        Assert.IsNotType<CreateContactPageView>(mainView.CurrentPage);
    }
}
