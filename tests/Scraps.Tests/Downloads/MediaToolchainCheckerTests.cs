using System.ComponentModel;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class MediaToolchainCheckerTests
{
    [Fact]
    public async Task CheckAsync_reports_tools_available_when_version_commands_succeed()
    {
        var runner = new ScriptedProcessRunner(_ => new ExternalProcessResult(0, "ffmpeg version 7.1", string.Empty));
        var checker = new MediaToolchainChecker(runner);

        var status = await checker.CheckAsync(CancellationToken.None);

        Assert.True(status.FfmpegAvailable);
        Assert.True(status.FfprobeAvailable);
        Assert.Equal(["ffmpeg", "ffprobe"], runner.Executables);
    }

    [Fact]
    public async Task CheckAsync_reports_tools_missing_when_executables_are_not_found()
    {
        var runner = new ScriptedProcessRunner(_ => throw new Win32Exception(2));
        var checker = new MediaToolchainChecker(runner);

        var status = await checker.CheckAsync(CancellationToken.None);

        Assert.False(status.FfmpegAvailable);
        Assert.False(status.FfprobeAvailable);
    }

    [Fact]
    public async Task CheckAsync_treats_failing_version_command_as_unavailable()
    {
        var runner = new ScriptedProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "broken build"));
        var checker = new MediaToolchainChecker(runner);

        var status = await checker.CheckAsync(CancellationToken.None);

        Assert.False(status.FfmpegAvailable);
        Assert.False(status.FfprobeAvailable);
    }

    private sealed class ScriptedProcessRunner(Func<string, ExternalProcessResult> respond) : IExternalProcessRunner
    {
        public List<string> Executables { get; } = [];

        public Task<ExternalProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<string>? standardOutputProgress = null,
            IProgress<string>? standardErrorProgress = null)
        {
            Executables.Add(executable);
            return Task.FromResult(respond(executable));
        }
    }
}
