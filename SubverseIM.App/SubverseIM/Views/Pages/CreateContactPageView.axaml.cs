using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
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

    private void InputPane_StateChanged(object? sender, InputPaneStateEventArgs e)
    {
        scrollView.VerticalScrollBarVisibility = e.NewState switch
        {
            InputPaneState.Open => ScrollBarVisibility.Auto,
            _ => ScrollBarVisibility.Disabled,
        };
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        await ((CreateContactPageViewModel)DataContext!).ApplyThemeOverrideAsync();

        IServiceManager serviceManager = ((CreateContactPageViewModel)DataContext!).ServiceManager;
        TopLevel topLevel = await serviceManager.GetWithAwaitAsync<TopLevel>();
        if (topLevel.InputPane is not null)
        {
            topLevel.InputPane.StateChanged += InputPane_StateChanged;
        }
    }
}