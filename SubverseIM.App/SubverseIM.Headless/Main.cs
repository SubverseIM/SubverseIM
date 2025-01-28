using Avalonia;
using Avalonia.Headless;
using SubverseIM.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
namespace SubverseIM.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder
            .Configure<App>(() => new App(,))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
