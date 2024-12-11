using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SubverseIM.Services;
using SubverseIM.ViewModels;
using SubverseIM.Views;
using System;

namespace SubverseIM;

public partial class App : Application
{
    private readonly IServiceManager? serviceManager;

    public App() : base() { }

    public App(IServiceManager? serviceManager) : this()
    {
        this.serviceManager = serviceManager;
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
                DataContext = new MainViewModel(serviceManager!)
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel(serviceManager!)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
