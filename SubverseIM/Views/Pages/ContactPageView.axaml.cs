using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SubverseIM.Views.Pages;

public partial class ContactPageView : UserControl
{
    private class PressTimerState
    {
        public bool HasElapsed { get; set; }
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

        tapTimerState = new();
        tapTimer = new Timer(TapTimerElapsed, tapTimerState,
            Timeout.Infinite, Timeout.Infinite);

        pressTimerState = new();
        pressTimer = new Timer(PressTimerElapsed, pressTimerState,
            Timeout.Infinite, Timeout.Infinite);

        contacts.SelectionChanged += Contacts_SelectionChanged;
    }

    private void PressTimerElapsed(object? state)
    {
        Debug.Assert(state == pressTimerState);
        lock (pressTimerState) { pressTimerState.HasElapsed = true; }
    }

    private void TapTimerElapsed(object? state)
    {
        Debug.Assert(state == tapTimerState);
        lock (tapTimerState) { tapTimerState.TapCount = 0; }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        lock (pressTimerState) { pressTimerState.HasElapsed = false; }
        pressTimer.Change(300, Timeout.Infinite);

        bool isFirstTap;
        lock (tapTimerState)
        {
            isFirstTap = tapTimerState.TapCount++ == 0;
        }

        if (isFirstTap)
        {
            tapTimer.Change(250, Timeout.Infinite);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        bool isLongPress;
        lock (pressTimerState) { isLongPress = pressTimerState.HasElapsed; }

        pressTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool isDoubleTap;
        lock (tapTimerState) { isDoubleTap = tapTimerState.TapCount > 1; }

        bool isLongPress;
        lock (pressTimerState) { isLongPress = pressTimerState.HasElapsed; }

        if (contacts.SelectedItems?.Count > 1)
        {
            foreach (var item in contacts.SelectedItems
                .Cast<ContactViewModel?>()
                .Where(x => x is not null)
                .Cast<ContactViewModel>())
            {
                item.ShouldShowOptions = false;
                item.IsSelected = false;
            }

            if (isDoubleTap)
            {
                lock (tapTimerState) { tapTimerState.TapCount = 0; }
            }

            if (isLongPress)
            {
                lock (pressTimerState) { pressTimerState.HasElapsed = false; }
            }

            if (isDoubleTap || isLongPress)
            {
                contacts.SelectedItems.Clear();
            }
        }

        foreach (var item in e.AddedItems
            .Cast<ContactViewModel?>()
            .Where(x => x is not null)
            .Cast<ContactViewModel>())
        {
            item.ShouldShowOptions = isDoubleTap;
            item.IsSelected = true;
        }

        foreach (var item in e.RemovedItems
            .Cast<ContactViewModel?>()
            .Where(x => x is not null)
            .Cast<ContactViewModel>())
        {
            item.ShouldShowOptions = isDoubleTap;
            item.IsSelected = isLongPress;
        }

        if (isLongPress && DataContext is not null)
        {
            await ((ContactPageViewModel)DataContext).MessageCommandAsync();
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((ContactPageViewModel)DataContext!).LoadContactsAsync();
    }
}