using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using ReactiveUI.Avalonia;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;

namespace SubverseIM.Android
{
    [Application(NetworkSecurityConfig = "@xml/network_security_config")]
    public class Application : AvaloniaAndroidApplication<App>
    {
        public IServiceManager ServiceManager { get; }

        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            ServiceManager = new ServiceManager();
        }

        protected override AppBuilder CreateAppBuilder()
        {
            return AppBuilder.Configure(
                () => new App(ServiceManager)
                ).UseAndroid();
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont()
                .UseReactiveUI();
        }
    }
}
