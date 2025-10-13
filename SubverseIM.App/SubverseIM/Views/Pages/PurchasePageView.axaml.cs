using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class PurchasePageView : UserControl
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    public Task LoadTask => loadTaskSource.Task;

    public PurchasePageView()
    {
        InitializeComponent();
        loadTaskSource = new();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.TrySetResult(e);

        await ((PurchasePageViewModel)DataContext!).ApplyThemeOverrideAsync();

        await ((PurchasePageViewModel)DataContext!).InitializeAsync();
    }
}