using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using SubverseIM.ViewModels;
using SubverseIM.Views.Pages;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Views;

public partial class MainView : NavigationPage
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    private TopLevel topLevel;

    public Task LoadTask => loadTaskSource.Task;

    public MainView()
    {
        InitializeComponent();
        loadTaskSource = new();
        topLevel = null!;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        topLevel = TopLevel.GetTopLevel(this) ??
           throw new InvalidOperationException("Could not resolve TopLevel instance from control");

        if (topLevel.InputPane is not null)
        {
            topLevel.InputPane.StateChanged += InputPaneStateChanged;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        ((MainViewModel)DataContext!).ServiceManager.GetOrRegister(topLevel);
        ((MainViewModel)DataContext!).ServiceManager.GetOrRegister<INavigation>(this);

        INavigationService navService = new NavigationService(((MainViewModel)DataContext!).ServiceManager);
        ((MainViewModel)DataContext!).ServiceManager.GetOrRegister(navService);

        await navService.NavigateLaunchedUriAsync();

        loadTaskSource.SetResult(e);
    }

    private void InputPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        VerticalAlignment = e.NewState switch
        {
            InputPaneState.Open => Avalonia.Layout.VerticalAlignment.Top,
            _ => Avalonia.Layout.VerticalAlignment.Stretch,
        };
        Height = Math.Max(e.EndRect.Top, 0);
    }

    public T? GetContentAs<T>()
        where T : class
    {
        return this.FindDescendantOfType<T>();
    }
}
