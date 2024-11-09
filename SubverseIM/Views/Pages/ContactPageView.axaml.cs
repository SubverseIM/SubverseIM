using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class ContactPageView : UserControl
{
    public ContactPageView()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await ((DataContext as ContactPageViewModel)?.LoadContactsAsync() ?? Task.CompletedTask);
    }
}