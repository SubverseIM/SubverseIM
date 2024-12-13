using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class ContactPageView : UserControl
{
    private class PressTimerState
    {
        public bool Elapsed { get; set; }
    }

    private class TapTimerState
    {
        public int TapCount { get; set; }
    }

    private readonly Timer pressTimer, tapTimer;

    private readonly PressTimerState pressTimerState;

    private readonly TapTimerState tapTimerState;

    public ContactPageView()
    {
        InitializeComponent();

        pressTimerState = new();
        pressTimer = new Timer(PressTimerElapsed, pressTimerState,
            Timeout.Infinite, Timeout.Infinite);

        tapTimerState = new();
        tapTimer = new Timer(TapTimerElapsed, tapTimerState,
            Timeout.Infinite, Timeout.Infinite);

        contacts.SelectionChanged += Contacts_SelectionChanged;
    }

    private async Task OpenMessagesAsync()
    {
        await ((ContactPageViewModel)DataContext!).MessageCommandAsync();
    }

    private void PressTimerElapsed(object? state)
    {
        Debug.Assert(state == pressTimerState);
        lock (pressTimerState) { pressTimerState.Elapsed = true; }
    }

    private void TapTimerElapsed(object? state)
    {
        Debug.Assert(state == tapTimerState);
        lock (tapTimerState) { tapTimerState.TapCount = 0; }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        lock (pressTimerState) { pressTimerState.Elapsed = false; }
        pressTimer.Change(300, Timeout.Infinite);
    }

    protected override async void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        pressTimer.Change(Timeout.Infinite, Timeout.Infinite);

        bool isFirstTap;
        lock (tapTimerState)
        {
            isFirstTap = tapTimerState.TapCount++ == 0;
        }

        if (isFirstTap)
        {
            tapTimer.Change(250, Timeout.Infinite);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(OpenMessagesAsync, DispatcherPriority.Input);
        }
    }

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool pressTimerElapsed;
        lock (pressTimerState) { pressTimerElapsed = pressTimerState.Elapsed; }

        bool isDoubleTap;
        lock (tapTimerState) { isDoubleTap = tapTimerState.TapCount > 0; }

        if (contacts.SelectedItems?.Count > 1)
        {
            foreach (var item in contacts.SelectedItems.Cast<ContactViewModel>())
            {
                item.ShouldShowOptions = false;
                item.IsSelected = !isDoubleTap;
            }
        }

        foreach (var item in e.AddedItems.Cast<ContactViewModel>())
        {
            item.ShouldShowOptions = pressTimerElapsed;
            item.IsSelected = true;
        }

        foreach (var item in e.RemovedItems.Cast<ContactViewModel>())
        {
            item.ShouldShowOptions = false;
            item.IsSelected = isDoubleTap;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((ContactPageViewModel)DataContext!).LoadContactsAsync();
    }
}