using Avalonia.Headless.XUnit;
using SubverseIM.ViewModels.Pages;

namespace SubverseIM.Headless;

public class MainViewTests : IClassFixture<MainViewFixture>
{
    private readonly MainViewFixture fixture;

    public MainViewTests(MainViewFixture fixture)
    {
        this.fixture = fixture;
    }

    [AvaloniaFact]
    public void ShouldStartInContactsView()
    {
        Assert.IsType<ContactPageViewModel>(fixture.GetViewModel().CurrentPage);
    }

    [AvaloniaFact]
    public void ShouldStartWithNoPreviousView()
    {
        Assert.False(fixture.GetViewModel().HasPreviousView);
    }
}
