using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    private readonly List<ContactViewModel> selectionStack;

    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    private readonly Timer pressTimer, tapTimer;

    private readonly PressTimerState pressTimerState;

    private readonly TapTimerState tapTimerState;

    private ILauncherService? launcherService;

    public Task LoadTask => loadTaskSource.Task;

    public ContactPageView()
    {
        InitializeComponent();

        selectionStack = new();
        loadTaskSource = new();

        pressTimerState = new();
        pressTimer = new Timer(PressTimerElapsed, pressTimerState,
            Timeout.Infinite, Timeout.Infinite);

        tapTimerState = new();
        tapTimer = new Timer(TapTimerElapsed, tapTimerState,
            Timeout.Infinite, Timeout.Infinite);

        contacts.PointerPressed += Contacts_PointerPressed;
        contacts.PointerReleased += Contacts_PointerReleased;

        contacts.SelectionChanged += Contacts_SelectionChanged;
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

        ContactPageViewModel? dataContext;
        lock (tapTimerState)
        {
            dataContext = tapTimerState.DataContext as ContactPageViewModel;
        }

        if (launcherService?.IsAccessibilityEnabled == false &&
            !isLongPress &&
            dataContext is not null &&
            dataContext.Parent is null &&
            contacts.SelectedItems?.Count > 0)
        {
            await Dispatcher.UIThread.InvokeAsync(dataContext.MessageCommand);
        }
        else
        {
            lock (tapTimerState) { tapTimerState.TapCount = 0; }
        }
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

    private void Contacts_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        pressTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool isLongPress;
        lock (pressTimerState) { isLongPress = pressTimerState.HasElapsed; }

        IEnumerable<object?> selectedItems = contacts.Items
            .Where(x => x is ContactViewModel vm && (vm.IsSelected || vm.ShouldShowOptions));
        if (selectedItems.Count() > 0)
        {
            foreach (var item in selectedItems
                .Where(x => !e.RemovedItems.Contains(x))
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
            item.ShouldShowOptions = true;
            item.IsSelected = true;
            selectionStack.Insert(0, item);
        }

        foreach (var item in e.RemovedItems
            .Cast<ContactViewModel?>()
            .Where(x => x is not null)
            .Cast<ContactViewModel>())
        {
            if (!item.ShouldShowOptions)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    contacts.Selection.Select(contacts.Items.IndexOf(item));
                    item.ShouldShowOptions = true;
                }, DispatcherPriority.Input);
            }
            else
            {
                item.ShouldShowOptions = false;
            }

            item.IsSelected = !isLongPress &&
                launcherService?.IsAccessibilityEnabled == false &&
                ((ContactPageViewModel)DataContext!).Parent is null;
            selectionStack.Remove(item);
        }

        if (!contacts.Items.Cast<ContactViewModel>().Any(x => x.ShouldShowOptions) && selectionStack.Count > 0)
        {
            selectionStack.First().ShouldShowOptions = true;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (loadTaskSource.TrySetResult(e))
        {
            ((ContactPageViewModel)DataContext!).Parent = null;
        }

        pressTimerState.DataContext = DataContext;
        tapTimerState.DataContext = DataContext;

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