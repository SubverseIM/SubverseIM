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
    private class TapTimerState
    {
        public int TapCount { get; set; }
    }

    private readonly Timer tapTimer;

    private readonly TapTimerState tapTimerState;

    public ContactPageView()
    {
        InitializeComponent();

        tapTimerState = new();
        tapTimer = new Timer(TapTimerElapsed, tapTimerState,
            Timeout.Infinite, Timeout.Infinite);

        contacts.SelectionChanged += Contacts_SelectionChanged;
    }

    private void TapTimerElapsed(object? state)
    {
        Debug.Assert(state == tapTimerState);
        lock (tapTimerState) { tapTimerState.TapCount = 0; }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

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

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool isDoubleTap;
        lock (tapTimerState) { isDoubleTap = tapTimerState.TapCount > 0; }

        if (contacts.SelectedItems?.Count > 1)
        {
            foreach (var item in contacts.SelectedItems.Cast<ContactViewModel>())
            {
                item.ShouldShowOptions = false;
            }

            if (isDoubleTap)
            {
                lock (tapTimerState) { tapTimerState.TapCount = 0; }
                contacts.SelectedItems.Clear();
            }
        }

        foreach (var item in e.AddedItems.Cast<ContactViewModel>())
        {
            item.ShouldShowOptions = isDoubleTap;
            item.IsSelected = true;
        }

        foreach (var item in e.RemovedItems.Cast<ContactViewModel>())
        {
            item.ShouldShowOptions = isDoubleTap;
            item.IsSelected = false;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((ContactPageViewModel)DataContext!).LoadContactsAsync();
    }
}