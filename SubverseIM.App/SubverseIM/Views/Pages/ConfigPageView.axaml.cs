using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class ConfigPageView : UserControl
{
    private TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    public Task LoadTask => loadTaskSource.Task;

    public ConfigPageView()
    {
        InitializeComponent();
        loadTaskSource = new();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.TrySetResult(e);

        await ((ConfigPageViewModel)DataContext!).InitializeAsync();

        if (((ConfigPageViewModel)DataContext!).PromptFreqIndex is null)
        {
            promptFreqBox.SelectedIndex = promptFreqBox.Items.Add("Never");
            promptFreqBox.IsEnabled = false;
        }
    }
}