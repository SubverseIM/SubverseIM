using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.ViewModels.Components;
using SubverseIM.ViewModels.Pages;
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
        await ((DataContext as MessagePageViewModel)?.InitializeAsync() ?? Task.CompletedTask);
    }
}