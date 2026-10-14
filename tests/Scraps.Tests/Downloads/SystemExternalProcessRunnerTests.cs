using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class SystemExternalProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_captures_standard_output_and_error()
    {
        var runner = new SystemExternalProcessRunner();

        var result = await runner.RunAsync("cmd.exe", ["/c", "echo stdout & echo stderr 1>&2"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("stdout", result.StandardOutput);
        Assert.Contains("stderr", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_preserves_process_exit_code()
    {
        var runner = new SystemExternalProcessRunner();

        var result = await runner.RunAsync("cmd.exe", ["/c", "exit 7"], CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_reports_stdout_lines_as_the_process_runs()
    {
        var runner = new SystemExternalProcessRunner();
        var progress = new InlineProgress();

        var result = await runner.RunAsync("cmd.exe", ["/c", "echo progress=continue"], CancellationToken.None, progress);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(progress.Lines, line => string.Equals(line.Trim(), "progress=continue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_reports_stderr_lines_as_the_process_runs()
    {
        var runner = new SystemExternalProcessRunner();
        var progress = new InlineProgress();

        var result = await runner.RunAsync("cmd.exe", ["/c", "echo progress=continue 1>&2"], CancellationToken.None,
            standardErrorProgress: progress);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(progress.Lines, line => string.Equals(line.Trim(), "progress=continue", StringComparison.Ordinal));
    }

    private sealed class InlineProgress : IProgress<string>
    {
        public List<string> Lines { get; } = [];
        public void Report(string value) => Lines.Add(value);
    }

    [Fact]
    public async Task RunAsync_cancellation_terminates_child_process()
    {
        var runner = new SystemExternalProcessRunner();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(Path.Combine(Environment.SystemDirectory, "ping.exe"), ["127.0.0.1", "-n", "30"], cancellation.Token));
    }
}
