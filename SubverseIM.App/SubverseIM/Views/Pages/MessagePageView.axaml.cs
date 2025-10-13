using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
using System.Collections.Specialized;
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

        messages.Items.CollectionChanged += MessageListChanged;

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
        if (DataContext is not null)
        {
            await ((MessagePageViewModel)DataContext).InitializeAsync();
        }
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

    private async void MessageListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is null || !LoadTask.IsCompleted) return;

        IServiceManager serviceManager = ((MessagePageViewModel)DataContext!).ServiceManager;
        IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();
        SubverseConfig config = await configurationService.GetConfigAsync();

        bool isValidAction = e.Action switch
        {
            NotifyCollectionChangedAction.Add => true,
            NotifyCollectionChangedAction.Reset => true,
            _ => false
        };

        if (config.MessageOrderFlag == true && isValidAction)
        {
            Dispatcher.UIThread.Post(() =>
                messages.ScrollIntoView(messages.ItemCount - 1),
                DispatcherPriority.Loaded
                );
        }
    }

    private async void TextBoxGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        IServiceManager serviceManager = ((MessagePageViewModel)DataContext!).ServiceManager;
        ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
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
        loadTaskSource.TrySetResult(e);

        await ((MessagePageViewModel)DataContext!).ApplyThemeOverrideAsync();

        IServiceManager serviceManager = ((MessagePageViewModel)DataContext!).ServiceManager;
        IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();
        SubverseConfig config = await configurationService.GetConfigAsync();

        if (config.MessageOrderFlag == true)
        {
            Dispatcher.UIThread.Post(() =>
                messages.ScrollIntoView(messages.ItemCount - 1),
                DispatcherPriority.Loaded
                );
        }

        splitView.IsPaneOpen = true;
        splitView.IsPaneOpen = false;
    }

    protected override async void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (DataContext is not null)
        {
            IServiceManager serviceManager = ((MessagePageViewModel)DataContext).ServiceManager;
            IConfigurationService configurationService = await serviceManager.GetWithAwaitAsync<IConfigurationService>();
            SubverseConfig config = await configurationService.GetConfigAsync();
            if (config.MessageOrderFlag == true) messages.ScrollIntoView(messages.ItemCount - 1);
        }
    }
}