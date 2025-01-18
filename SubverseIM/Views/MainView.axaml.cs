using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SubverseIM.ViewModels;
using System;

namespace SubverseIM.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        TopLevel topLevel = TopLevel.GetTopLevel(this) ??
           throw new InvalidOperationException("Could not resolve TopLevel instance from control");
        ((MainViewModel)DataContext!).RegisterTopLevel(topLevel);

        ((MainViewModel)DataContext!).ScreenOrientationChangedDelegate ??= ScreenOrientationChanged;
        if (topLevel.Screens is not null)
        {
            topLevel.Screens.Changed += (s, ev) => ScreenOrientationChanged();
        }

        ((MainViewModel)DataContext!).NavigateLaunchedUri();
    }

    public void ScreenOrientationChanged()
    {
        ((MainViewModel)DataContext!).CurrentPage
            .OnOrientationChanged(TopLevel.GetTopLevel(this));
    }
}
