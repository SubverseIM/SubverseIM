using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class TorrentPageView : UserControl
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    public Task LoadTask => loadTaskSource.Task;

    public TorrentPageView()
    {
        InitializeComponent();
        loadTaskSource = new();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.TrySetResult(e);

        await ((TorrentPageViewModel)DataContext!).ApplyThemeOverrideAsync();

        await ((TorrentPageViewModel)DataContext!).InitializeAsync();
    }
}