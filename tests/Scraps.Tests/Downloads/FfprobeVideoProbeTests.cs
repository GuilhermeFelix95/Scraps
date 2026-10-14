using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class FfprobeVideoProbeTests
{
    [Fact]
    public async Task ProbeAsync_reads_video_dimensions_and_duration_from_json()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(0,
            """{"streams":[{"codec_type":"video","width":1920,"height":1080}],"format":{"duration":"12.5"}}""", ""));
        var probe = new FfprobeVideoProbe(runner);

        var info = await probe.ProbeAsync("sample.mp4", CancellationToken.None);

        Assert.Equal(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(12.5)), info);
    }

    [Fact]
    public async Task ProbeAsync_rejects_input_without_video_stream()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(0,
            """{"streams":[{"codec_type":"audio"}],"format":{"duration":"2"}}""", ""));
        var probe = new FfprobeVideoProbe(runner);

        await Assert.ThrowsAsync<InvalidDataException>(() => probe.ProbeAsync("audio.mp4", CancellationToken.None));
    }

    [Fact]
    public async Task ProbeAsync_reports_process_failure()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(1, "", "corrupt input"));
        var probe = new FfprobeVideoProbe(runner);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => probe.ProbeAsync("bad.mp4", CancellationToken.None));

        Assert.Contains("corrupt input", error.Message);
    }

    private sealed class StubProcessRunner(ExternalProcessResult result) : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken, IProgress<string>? standardOutputProgress = null, IProgress<string>? standardErrorProgress = null) =>
            Task.FromResult(result);
    }
}
