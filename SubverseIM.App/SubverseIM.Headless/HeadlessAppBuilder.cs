using Avalonia;
using Avalonia.Headless;
using SubverseIM.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace SubverseIM.Headless;

public class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
        .UseHarfBuzz();
}
