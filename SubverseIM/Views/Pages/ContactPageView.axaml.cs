using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class ContactPageView : UserControl
{
    public ContactPageView()
    {
        InitializeComponent();
        contacts.SelectionChanged += Contacts_SelectionChanged;
    }

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
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
            else if (e.AddedItems.Count > 0)
            {
                foreach (var other in contacts.Items.Cast<ContactViewModel>())
                {
                    other.IsDoubleSelected = false;
                }
            }
        }
        catch (InvalidOperationException) { }

        foreach (var item in e.AddedItems.Cast<ContactViewModel>()) 
        {
            item.IsSelected = true;
        }

        foreach (var item in e.RemovedItems.Cast<ContactViewModel>())
        {
            item.IsSelected = false;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((DataContext as ContactPageViewModel)?.LoadContactsAsync() ?? Task.CompletedTask);
    }
}