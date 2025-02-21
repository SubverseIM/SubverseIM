using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SubverseIM.ViewModels;
using System;
using System.Linq;
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

        ((MainViewModel)DataContext!).NavigateLaunchedUri();
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
        ((MainViewModel)DataContext!).CurrentPage
            .OnOrientationChanged(TopLevel.GetTopLevel(this));
    }

    public T? GetContentAs<T>()
        where T : class
    {
        return contentControl.FindDescendantOfType<T>();
    }
}
