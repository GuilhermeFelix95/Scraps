using System.ComponentModel;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class YtDlpUpdaterTests
{
    private static readonly string BundledDirectory = Path.Combine(Path.GetTempPath(), "app", "third-party");

    [Fact]
    public async Task UpdateAsync_runs_self_update_for_bundled_binary()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(0, "yt-dlp is up to date (2026.08.19)", string.Empty));
        var updater = new YtDlpUpdater(runner, BundledDirectory);
        var executable = Path.Combine(BundledDirectory, "yt-dlp", "yt-dlp.exe");

        var result = await updater.UpdateAsync(executable, CancellationToken.None);

        Assert.Equal(ToolUpdateStatus.Succeeded, result.Status);
        Assert.Contains("up to date", result.Message);
        Assert.Equal(["-U"], runner.Arguments);
        Assert.Equal(executable, runner.Executable);
    }

    [Fact]
    public async Task UpdateAsync_reports_failure_without_throwing_when_update_fails()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(1, string.Empty, "network unreachable"));
        var updater = new YtDlpUpdater(runner, BundledDirectory);

        var result = await updater.UpdateAsync(Path.Combine(BundledDirectory, "yt-dlp", "yt-dlp.exe"), CancellationToken.None);

        Assert.Equal(ToolUpdateStatus.Failed, result.Status);
        Assert.Contains("network unreachable", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_skips_yt_dlp_installed_outside_the_bundled_folder()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(0, "should not run", string.Empty));
        var updater = new YtDlpUpdater(runner, BundledDirectory);

        var result = await updater.UpdateAsync(@"C:\Program Files\yt-dlp\yt-dlp.exe", CancellationToken.None);

        Assert.Equal(ToolUpdateStatus.Skipped, result.Status);
        Assert.Null(runner.Arguments);
    }

    [Fact]
    public async Task UpdateAsync_skips_bare_name_resolved_from_path()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(0, "should not run", string.Empty));
        var updater = new YtDlpUpdater(runner, BundledDirectory);

        var result = await updater.UpdateAsync("yt-dlp.exe", CancellationToken.None);

        Assert.Equal(ToolUpdateStatus.Skipped, result.Status);
        Assert.Null(runner.Arguments);
    }

    [Fact]
    public async Task UpdateAsync_reports_failure_when_executable_cannot_start()
    {
        var runner = new StubProcessRunner(exception: new Win32Exception(2, "missing binary"));
        var updater = new YtDlpUpdater(runner, BundledDirectory);

        var result = await updater.UpdateAsync(Path.Combine(BundledDirectory, "yt-dlp", "yt-dlp.exe"), CancellationToken.None);

        Assert.Equal(ToolUpdateStatus.Failed, result.Status);
        Assert.Contains("missing binary", result.Message);
    }

    private sealed class StubProcessRunner(ExternalProcessResult? result = null, Exception? exception = null) : IExternalProcessRunner
    {
        public IReadOnlyList<string>? Arguments { get; private set; }
        public string? Executable { get; private set; }

        public Task<ExternalProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<string>? standardOutputProgress = null,
            IProgress<string>? standardErrorProgress = null)
        {
            Executable = executable;
            Arguments = arguments.ToArray();
            if (exception is not null) throw exception;
            return Task.FromResult(result!);
        }
    }
}
