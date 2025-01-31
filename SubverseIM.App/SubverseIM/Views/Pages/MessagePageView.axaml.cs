using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class MessagePageView : UserControl
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    public Task LoadTask => loadTaskSource.Task;

    public MessagePageView()
    {
        InitializeComponent();
        loadTaskSource = new();

        contacts.SelectionChanged += ContactsSelectionChanged;
        messages.SelectionChanged += MessagesSelectionChanged;
        topicBox.SelectionChanged += TopicBoxSelectionChanged;

        messageBox.GotFocus += TextBoxGotFocus;
    }

    private void ContactsSelectionChanged(object? sender, SelectionChangedEventArgs e)
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

    private async void TopicBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await ((MessagePageViewModel)DataContext!).InitializeAsync();
    }

    private void MessagesSelectionChanged(object? sender, SelectionChangedEventArgs e)
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
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.SetResult(e);

        await ((MessagePageViewModel)DataContext!).InitializeAsync();
        ((MessagePageViewModel)DataContext!).RaisePropertyChanged(
            nameof(MessagePageViewModel.SendMessageTopicName)
            );
    }
}