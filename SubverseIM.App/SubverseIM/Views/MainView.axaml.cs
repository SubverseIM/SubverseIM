﻿using Avalonia.Controls;
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

    public T? GetContentAs<T>()
        where T : class
    {
        return contentControl.FindDescendantOfType<T>();
    }
}
