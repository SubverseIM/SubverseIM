using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SubverseIM.ViewModels;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Views;

public partial class MainView : NavigationPage
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    private TopLevel? topLevel;

    public Task LoadTask => loadTaskSource.Task;

    public MainView()
    {
        InitializeComponent();
        loadTaskSource = new();
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

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.SetResult(e);

        ((MainViewModel)DataContext!).ServiceManager.GetOrRegister(topLevel!);

        _ = ((MainViewModel)DataContext!).NavigateLaunchedUriAsync();
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
