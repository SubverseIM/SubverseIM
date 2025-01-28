using Avalonia;
using Avalonia.Headless;
using SubverseIM.Headless.Services;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using SubverseIM.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]
namespace SubverseIM.Tests
{
    public class HeadlessAppBuilder
    {
        public static IServiceManager GetServiceManager() 
        {
            IServiceManager serviceManager = new ServiceManager();
            serviceManager.GetOrRegister<FauxDbService, IDbService>();
            return serviceManager;
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder
            .Configure(() => new App(GetServiceManager()))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
