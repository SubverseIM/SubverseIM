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

        topLevel.SizeChanged += RootLayoutUpdated;
        RootLayoutUpdated(topLevel, new EventArgs());

        ((MainViewModel)DataContext!).NavigateLaunchedUri();
    }

    private void RootLayoutUpdated(object? sender, EventArgs e)
    {
        ((MainViewModel)DataContext!).CurrentPage.OnOrientationChanged(sender as TopLevel);
    }
}
