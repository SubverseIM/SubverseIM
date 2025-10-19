using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SubverseIM.ViewModels;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Views;

public partial class MainView : UserControl
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    public Task LoadTask => loadTaskSource.Task;

    public MainView()
    {
        InitializeComponent();
        loadTaskSource = new();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.SetResult(e);

        TopLevel topLevel = TopLevel.GetTopLevel(this) ??
           throw new InvalidOperationException("Could not resolve TopLevel instance from control");
        ((MainViewModel)DataContext!).RegisterTopLevel(topLevel);

        if (topLevel.InputPane is not null)
        {
            topLevel.InputPane.StateChanged += InputPaneStateChanged;
        }

        if (topLevel.Screens is not null)
        {
            topLevel.Screens.Changed += (s, ev) => ScreenOrientationChanged();
        }
        ((MainViewModel)DataContext!).ScreenOrientationChangedDelegate ??= ScreenOrientationChanged;

        _ = ((MainViewModel)DataContext!).NavigateLaunchedUriAsync();
    }

    private void InputPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        VerticalAlignment = e.NewState switch
        {
            InputPaneState.Open => Avalonia.Layout.VerticalAlignment.Top,
            _ => Avalonia.Layout.VerticalAlignment.Stretch,
        };
        Height = ((IInputPane?)sender)?.OccludedRect.Top ?? Height;
    }

    public void ScreenOrientationChanged()
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        Width = topLevel?.Width ?? Width;
        Height = topLevel?.Height ?? Height;

        ((MainViewModel)DataContext!).CurrentPage
            .OnOrientationChanged(topLevel);
    }

    public T? GetContentAs<T>()
        where T : class
    {
        return contentControl.FindDescendantOfType<T>();
    }
}
