using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Scraps.Desktop.ViewModels;

namespace Scraps.Desktop;

// Avalonia.Application qualificado: dentro deste namespace, "Application" resolve para Scraps.Application.
public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}