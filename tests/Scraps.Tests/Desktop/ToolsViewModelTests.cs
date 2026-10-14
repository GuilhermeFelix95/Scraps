using Scraps.Application.Downloads;
using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Desktop;

public sealed class ToolsViewModelTests
{
    [Fact]
    public void Shows_availability_and_the_same_warnings_as_the_cli()
    {
        var toolchain = new MediaToolchainStatus(FfmpegAvailable: false, FfprobeAvailable: true);

        var viewModel = new ToolsViewModel(toolchain, new YtDlpUpdater(new ThrowingRunner(), "C:/bundled"), "C:/bundled/yt-dlp.exe");

        Assert.Equal("Não encontrado", viewModel.FfmpegStatus);
        Assert.Equal("Disponível", viewModel.FfprobeStatus);
        Assert.Equal(ToolchainWarnings.From(toolchain), viewModel.Warnings);
    }

    [Fact]
    public async Task Check_reports_the_bundled_ytdlp_update_result()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var bundled = Path.Combine(root.FullName, "third-party");
            Directory.CreateDirectory(bundled);
            var executable = Path.Combine(bundled, "yt-dlp.exe");
            var runner = new FixedRunner(new ExternalProcessResult(0, "yt-dlp is up to date (2026.10.01)", string.Empty));
            var viewModel = new ToolsViewModel(
                new MediaToolchainStatus(FfmpegAvailable: true, FfprobeAvailable: true),
                new YtDlpUpdater(runner, bundled),
                executable);
            Assert.Equal("Verificando atualização do yt-dlp...", viewModel.YtDlpStatus);

            await viewModel.CheckYtDlpAsync(CancellationToken.None);

            Assert.Equal("yt-dlp is up to date (2026.10.01)", viewModel.YtDlpStatus);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task External_ytdlp_is_reported_without_running_an_update()
    {
        var viewModel = new ToolsViewModel(
            new MediaToolchainStatus(FfmpegAvailable: true, FfprobeAvailable: true),
            new YtDlpUpdater(new ThrowingRunner(), "C:/bundled"),
            "C:/tools/yt-dlp.exe");

        await viewModel.CheckYtDlpAsync(CancellationToken.None);

        Assert.Equal("yt-dlp externo: atualização automática desativada.", viewModel.YtDlpStatus);
    }

    private sealed class FixedRunner(ExternalProcessResult result) : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken, IProgress<string>? standardOutput, IProgress<string>? standardError) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingRunner : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken, IProgress<string>? standardOutput, IProgress<string>? standardError) =>
            throw new InvalidOperationException("O yt-dlp externo não deve ser executado pela atualização.");
    }
}
