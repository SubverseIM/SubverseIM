using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SubverseIM.Views.Pages;

public partial class ContactPageView : UserControl
{
    private class TimerState 
    {
        public object? DataContext { get; set; }
    }

    private class PressTimerState : TimerState
    {
        public bool HasElapsed { get; set; }
    }

    private class TapTimerState : TimerState
    {
        public int TapCount { get; set; }
    }

    private readonly Timer pressTimer, tapTimer;

    private readonly PressTimerState pressTimerState;

    private readonly TapTimerState tapTimerState;

    private ILauncherService? launcherService;

    public ContactPageView()
    {
        InitializeComponent();

        tapTimerState = new();
        tapTimer = new Timer(TapTimerElapsed, tapTimerState,
            Timeout.Infinite, Timeout.Infinite);

        pressTimerState = new();
        pressTimer = new Timer(PressTimerElapsed, pressTimerState,
            Timeout.Infinite, Timeout.Infinite);

        topics.SelectionChanged += Topics_SelectionChanged;

        contacts.SelectionChanged += Contacts_SelectionChanged;
        contacts.PointerPressed += Contacts_PointerPressed;
        contacts.PointerReleased += Contacts_PointerReleased;
    }

    private void Contacts_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        pressTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Contacts_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        lock (pressTimerState) { pressTimerState.HasElapsed = false; }
        pressTimer.Change(275, Timeout.Infinite);

        bool isFirstTap;
        lock (tapTimerState)
        {
            isFirstTap = tapTimerState.TapCount++ == 0;
        }

        if (isFirstTap)
        {
            tapTimer.Change(300, Timeout.Infinite);
        }
    }

    private void PressTimerElapsed(object? state)
    {
        Debug.Assert(state == pressTimerState);
        lock (pressTimerState) { pressTimerState.HasElapsed = true; }
    }

    private async void TapTimerElapsed(object? state)
    {
        Debug.Assert(state == tapTimerState);

        bool isLongPress;
        lock (pressTimerState) { isLongPress = pressTimerState.HasElapsed; }

        bool isDoubleTap;
        ContactPageViewModel? dataContext;
        lock (tapTimerState) 
        { 
            isDoubleTap = tapTimerState.TapCount > 1;
            dataContext = tapTimerState.DataContext as ContactPageViewModel;
        }

        if (launcherService?.IsAccessibilityEnabled == false &&
            !isLongPress && 
            !isDoubleTap && 
            dataContext is not null && 
            dataContext.Parent is null)
        {
            await Dispatcher.UIThread.InvokeAsync(dataContext.MessageCommandAsync);
        }
        else
        {
            lock (tapTimerState) { tapTimerState.TapCount = 0; }
        }
    }

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool isDoubleTap;
        lock (tapTimerState) { isDoubleTap = tapTimerState.TapCount > 1; }

        bool isLongPress;
        lock (pressTimerState) { isLongPress = pressTimerState.HasElapsed; }

        IEnumerable<object?> selectedItems = contacts.Items
            .Where(x => x is ContactViewModel vm && (vm.IsSelected || vm.ShouldShowOptions));
        if (selectedItems.Count() > 0)
        {
            foreach (var item in selectedItems
                .Cast<ContactViewModel>())
            {
                item.ShouldShowOptions = false;
            }

            if (isLongPress)
            {
                lock (pressTimerState) { pressTimerState.HasElapsed = false; }
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
            item.IsSelected = !isLongPress && 
                launcherService?.IsAccessibilityEnabled == false &&
                ((ContactPageViewModel)DataContext!).Parent is null;
        }
    }

    private void Topics_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems.Cast<TopicViewModel>()) 
        {
            item.IsSelected = true;
        }

        foreach (var item in e.RemovedItems.Cast<TopicViewModel>())
        {
            item.IsSelected = false;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        pressTimerState.DataContext = DataContext;
        tapTimerState.DataContext = DataContext;

        await ((ContactPageViewModel)DataContext!).LoadTopicsAsync();
        await ((ContactPageViewModel)DataContext!).LoadContactsAsync();

        ILauncherService launcherService = await ((ContactPageViewModel)DataContext!)
            .ServiceManager.GetWithAwaitAsync<ILauncherService>();
        this.launcherService = launcherService;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        ((ContactPageViewModel)DataContext!).Parent = null;
    }
}