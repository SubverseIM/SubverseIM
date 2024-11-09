using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class MessagePageView : UserControl
{
    public MessagePageView()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((DataContext as MessagePageViewModel)?.InitializeAsync() ?? Task.CompletedTask);
    }
}