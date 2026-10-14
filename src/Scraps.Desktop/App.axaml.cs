using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

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
            var mainViewModel = new MainViewModel(CreateServicesAsync);
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
            _ = mainViewModel.InitializeAsync();
            desktop.Exit += (_, _) => mainViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Mesmas variáveis da CLI: SCRAPS_FFMPEG, SCRAPS_YTDLP e SCRAPS_YTDLP_JS_RUNTIME.
    private static Task<ScrapsServices> CreateServicesAsync(CancellationToken cancellationToken) =>
        ScrapsComposition.CreateAsync(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("SCRAPS_FFMPEG"),
            Environment.GetEnvironmentVariable("SCRAPS_YTDLP"),
            Environment.GetEnvironmentVariable("SCRAPS_YTDLP_JS_RUNTIME"),
            cancellationToken);
}
