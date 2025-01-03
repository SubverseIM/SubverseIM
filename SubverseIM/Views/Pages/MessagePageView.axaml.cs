using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class MessagePageView : UserControl
{
    public MessagePageView()
    {
        InitializeComponent();
        messages.SelectionChanged += Messages_SelectionChanged;
        topicBox.SelectionChanged += TopicBox_SelectionChanged;
        contacts.SelectionChanged += Contacts_SelectionChanged;

        messageBox.GotFocus += TextBoxGotFocus;
        messageBox.LostFocus += TextBoxLostFocus;
    }

    private void Contacts_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (contacts.ItemCount == 1) return;

        foreach (var contact in e.AddedItems
            .Cast<ContactViewModel?>()
            .Where(x => x is not null)
            .Cast<ContactViewModel>())
        {
            contact.ShouldShowOptions = true;
        }

        foreach (var contact in e.RemovedItems
            .Cast<ContactViewModel?>()
            .Where(x => x is not null)
            .Cast<ContactViewModel>())
        {
            contact.ShouldShowOptions = false;
        }
    }

    private async void TextBoxGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        ILauncherService launcherService = await ((MessagePageViewModel)DataContext!)
            .ServiceManager.GetWithAwaitAsync<ILauncherService>();

        if (launcherService.IsAccessibilityEnabled && sender is TextBox textBox)
        {
            textBox.IsEnabled = false;

            string? messageText = await launcherService.ShowInputDialogAsync(
                textBox.Watermark ?? "Enter Input Text", textBox.Text
                );
            textBox.Text = messageText;

            textBox.IsEnabled = true;
        }

        ((MessagePageViewModel)DataContext!).MessageTextDock = Dock.Top;
    }

    private void TextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(messageBox.Text))
        {
            ((MessagePageViewModel)DataContext!).MessageTextDock = Dock.Bottom;
        }
    }

    private async void TopicBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await ((DataContext as MessagePageViewModel)?.InitializeAsync() ?? Task.CompletedTask);
    }

    private void Messages_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems.Cast<MessageViewModel>())
        {
            item.IsSelected = true;
        }

        foreach (var item in e.RemovedItems.Cast<MessageViewModel>())
        {
            item.IsSelected = false;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((MessagePageViewModel)DataContext!).InitializeAsync();
        ((MessagePageViewModel)DataContext!).RaisePropertyChanged(
            nameof(MessagePageViewModel.SendMessageTopicName)
            );
    }
}