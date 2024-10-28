using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.Views;

namespace SubverseIM;

public partial class App : Application
{
    private readonly IServiceManager<IPeerService> peerServiceManager;

    public App(IServiceManager<IPeerService> peerServiceManager) 
    {
        this.peerServiceManager = peerServiceManager;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
