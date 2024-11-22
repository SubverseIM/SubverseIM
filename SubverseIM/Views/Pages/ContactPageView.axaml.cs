using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class ContactPageView : UserControl
{
    private readonly Timer pressTimer;

    private bool timerElapsed;

    public ContactPageView()
    {
        InitializeComponent();

        pressTimer = new Timer(PressTimerElapsed);
        contacts.SelectionChanged += Contacts_SelectionChanged;
    }

    private async Task OpenMessagesAsync()
    {
        await ((ContactPageViewModel)DataContext!).MessageCommandAsync();
    }

    private void PressTimerElapsed(object? state) 
    {
        timerElapsed = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        pressTimer.Change(250, Timeout.Infinite);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (timerElapsed)
        {
            Dispatcher.UIThread.Invoke(OpenMessagesAsync, DispatcherPriority.Input);
        }

        pressTimer.Change(Timeout.Infinite, Timeout.Infinite);
        timerElapsed = false;
    }

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool suppressFlag = false;
        try
        {
            ContactViewModel? item = e.RemovedItems
                .Cast<ContactViewModel>()
                .SingleOrDefault();
            if (item?.IsDoubleSelected == false)
            {
                foreach (var other in contacts.Items.Cast<ContactViewModel>())
                {
                    contacts.SelectedItems?.Remove(other);
                    other.IsDoubleSelected = false;
                }
                item.IsDoubleSelected = true;
            }
            else if (e.AddedItems.Count == 1)
            {
                item = e.AddedItems
                    .Cast<ContactViewModel>()
                    .Single();
                if (item.IsDoubleSelected)
                {
                    contacts.SelectedItems?.Remove(item);
                    item.IsDoubleSelected = false;
                    suppressFlag = true;
                }
            }

            if (e.AddedItems.Count > 0)
            {
                foreach (var other in contacts.Items.Cast<ContactViewModel>())
                {
                    other.IsDoubleSelected = false;
                }
            }
        }
        catch (InvalidOperationException) { }

        if (!suppressFlag)
        {
            foreach (var item in e.AddedItems.Cast<ContactViewModel>())
            {
                item.IsSelected = true;
            }

            foreach (var item in e.RemovedItems.Cast<ContactViewModel>())
            {
                item.IsSelected = false;
            }
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((ContactPageViewModel)DataContext!).LoadContactsAsync();
    }
}