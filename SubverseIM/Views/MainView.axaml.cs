using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SubverseIM.ViewModels;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        IStorageProvider storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider ?? 
            throw new InvalidOperationException("StorageProvider could not be fetched.");
        (DataContext as MainViewModel)?.RegisterStorageProvider(storageProvider);

        (DataContext as MainViewModel)?.NavigateLaunchedUri();
    }
}
