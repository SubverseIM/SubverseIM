using Avalonia.Headless.XUnit;
using SubverseIM.Headless.Fixtures;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views;
using SubverseIM.Views.Pages;
using System.Diagnostics;

namespace SubverseIM.Headless.Tests.Pages;

public class ConfigPageViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public ConfigPageViewTests(MainViewFixture fixture)
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

    private async Task<(ConfigPageView, ConfigPageViewModel)> EnsureIsOnConfigPageView()
    {
        MainView mainView = await EnsureMainViewLoaded();

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        while (mainViewModel.HasPreviousView && await mainViewModel.NavigatePreviousViewAsync(shouldForceNavigation: true)) ;

        await mainViewModel.NavigateConfigViewAsync();

        ConfigPageViewModel? configPageViewModel = mainViewModel.CurrentPage as ConfigPageViewModel;
        Assert.NotNull(configPageViewModel);

        ConfigPageView? configPageView = mainView.GetContentAs<ConfigPageView>();
        Assert.NotNull(configPageView);

        await configPageView.LoadTask;

        return (configPageView, configPageViewModel);
    }

    [AvaloniaFact]
    public async Task ShouldLoadBootstrapperUriList()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        Assert.NotEmpty(configPageViewModel.BootstrapperUriList);
    }

    [AvaloniaFact]
    public async Task ShouldAddBootstrapperUri()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        await configPageViewModel.AddBootstrapperUriCommand("https://www.example.com/");
        Assert.Contains("https://www.example.com/", configPageViewModel.BootstrapperUriList);

        configPageViewModel.BootstrapperUriList.Remove("https://www.example.com/");
    }

    [AvaloniaFact]
    public async Task ShouldRemoveBootstrapperUri()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        configPageViewModel.BootstrapperUriList.Add("https://www.example.com/");

        configPageViewModel.SelectedUriList.Add("https://www.example.com/");
        configPageViewModel.RemoveBootstrapperUriCommand();

        Assert.DoesNotContain("https://www.example.com/", configPageViewModel.BootstrapperUriList);
    }

    [AvaloniaFact]
    public async Task ShouldSaveChangesIfConfigValid() 
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        configPageViewModel.BootstrapperUriList.Add("https://www.example.com/");

        bool result = await configPageViewModel.SaveConfigurationCommand();
        Assert.True(result);

        configPageViewModel.BootstrapperUriList.Remove("https://www.example.com/");
    }

    [AvaloniaFact]
    public async Task ShouldNotSaveChangesIfConfigInvalid_ReasonEmpty()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        configPageViewModel.BootstrapperUriList.Clear();

        bool result = await configPageViewModel.SaveConfigurationCommand();
        Assert.False(result);

        configPageViewModel.BootstrapperUriList.Add(IBootstrapperService.DEFAULT_BOOTSTRAPPER_ROOT);
    }

    [AvaloniaFact]
    public async Task ShouldNotSaveChangesIfConfigInvalid_ReasonInvalidUri()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        configPageViewModel.BootstrapperUriList.Add("hello world");

        bool result = await configPageViewModel.SaveConfigurationCommand();
        Assert.False(result);

        configPageViewModel.BootstrapperUriList.Remove("hello world");
    }

    [AvaloniaFact]
    public async Task ShouldReturnToPreviousViewOnValidSave() 
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        bool result = await configPageViewModel.SaveConfigurationCommand();
        Debug.Assert(result == true); // This should always be true. If not, the test needs to be rewritten.

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        Assert.IsNotType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNotReturnToPreviousViewOnInvalidSave()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        configPageViewModel.BootstrapperUriList.Add("invalid state");

        bool result = await configPageViewModel.SaveConfigurationCommand();
        Debug.Assert(result == false); // This should always be false. If not, the test needs to be rewritten.

        configPageViewModel.BootstrapperUriList.Remove("invalid state");

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        Assert.IsType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }

    [AvaloniaFact]
    public async Task ShouldNavigateToPreviousView()
    {
        (ConfigPageView configPageView, ConfigPageViewModel configPageViewModel) =
            await EnsureIsOnConfigPageView();

        IServiceManager serviceManager = await fixture.GetServiceManagerAsync();
        IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
        await frontendService.NavigatePreviousViewAsync(shouldForceNavigation: true);

        MainViewModel mainViewModel = await fixture.GetViewModelAsync();
        Assert.IsNotType<ConfigPageViewModel>(mainViewModel.CurrentPage);
    }
}
