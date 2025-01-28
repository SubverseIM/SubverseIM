using Avalonia.Controls;
using SubverseIM.Services;
using SubverseIM.ViewModels.Pages;

namespace SubverseIM.Views.Pages;

public partial class CreateContactPageView : UserControl
{
    public CreateContactPageView()
    {
        InitializeComponent();

        nameEditBox.GotFocus += TextBoxGotFocus;
        noteEditBox.GotFocus += TextBoxGotFocus;
    }

    private async void TextBoxGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        ILauncherService launcherService = await (DataContext as CreateContactPageViewModel)!
            .ServiceManager.GetWithAwaitAsync<ILauncherService>();

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
}