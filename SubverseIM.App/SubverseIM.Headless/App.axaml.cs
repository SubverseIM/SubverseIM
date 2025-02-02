using Avalonia;
using Avalonia.Markup.Xaml;

namespace SubverseIM.Headless;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}