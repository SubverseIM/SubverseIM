using Avalonia.Controls;
using Avalonia.Interactivity;
using SubverseIM.Headless.Components;
using SubverseIM.ViewModels.Pages;
using System.Threading.Tasks;

namespace SubverseIM.Views.Pages;

public partial class UploadPageView : UserControl
{
    private readonly TaskCompletionSource<RoutedEventArgs> loadTaskSource;

    public Task LoadTask => loadTaskSource.Task;

    public UploadPageView()
    {
        InitializeComponent();
        loadTaskSource = new();

        uploadListBox.SelectionChanged += UploadSelectionChanged;
    }

    private void UploadSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (UploadTaskViewModel uploadTask in e.AddedItems) 
        {
            uploadTask.IsSelected = true;
        }

        foreach (UploadTaskViewModel uploadTask in e.RemovedItems) 
        {
            uploadTask.IsSelected = false;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        loadTaskSource.TrySetResult(e);

        await ((UploadPageViewModel)DataContext!).ApplyThemeOverrideAsync();

        await ((UploadPageViewModel)DataContext!).InitializeAsync();
    }
}