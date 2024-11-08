using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.ViewModels;
using System.Threading.Tasks;

namespace SubverseIM.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        await ((DataContext as MainViewModel)?.InvokeFromLauncherAsync() ?? Task.CompletedTask);
    }
}
