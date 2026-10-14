using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class FfmpegVideoTranscoderTests
{
    [Fact]
    public async Task TranscodeAsync_uses_safe_ffmpeg_arguments_and_selected_height()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "input with spaces.mp4");
            var destination = Path.Combine(root.FullName, "output.mp4");
            await File.WriteAllTextAsync(source, "source");
            var runner = new StubProcessRunner(onRun: args => File.WriteAllText(args[^1], "encoded"));
            var transcoder = new FfmpegVideoTranscoder(runner);

            var result = await transcoder.TranscodeAsync(source, destination, new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)), VideoQualityPreset.P720, null, CancellationToken.None);

            Assert.Equal(destination, result);
            Assert.Contains("-nostdin", runner.Arguments);
            Assert.Contains("-n", runner.Arguments);
            Assert.Contains("-progress", runner.Arguments);
            Assert.Contains("pipe:1", runner.Arguments);
            Assert.Contains("libx264", runner.Arguments);
            Assert.Contains("aac", runner.Arguments);
            Assert.Contains("scale=-2:720", runner.Arguments);
            Assert.Equal(source, runner.Arguments[Array.IndexOf(runner.Arguments.ToArray(), "-i") + 1]);
            Assert.EndsWith(".partial.mp4", runner.Arguments[^1]);
            Assert.True(File.Exists(destination));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task TranscodeAsync_runs_the_configured_ffmpeg_executable()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            await File.WriteAllBytesAsync(source, [1, 2, 3]);
            var runner = new StubProcessRunner(onRun: args => File.WriteAllText(args[^1], "encoded"));
            var transcoder = new FfmpegVideoTranscoder(runner, @"C:\tools\ffmpeg.exe");

            await transcoder.TranscodeAsync(source, Path.Combine(root.FullName, "out.mp4"),
                new VideoInfo(1920, 1080, TimeSpan.FromSeconds(1)), VideoQualityPreset.P720, null, CancellationToken.None);

            Assert.Equal(@"C:\tools\ffmpeg.exe", runner.Executable);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task TranscodeAsync_original_copies_source_bytes_without_invoking_ffmpeg()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mov");
            var destination = Path.Combine(root.FullName, "copy.mov");
            var bytes = new byte[] { 0, 1, 2, 255, 17 };
            await File.WriteAllBytesAsync(source, bytes);
            var runner = new StubProcessRunner();
            var transcoder = new FfmpegVideoTranscoder(runner);

            var result = await transcoder.TranscodeAsync(source, destination, new VideoInfo(640, 480, TimeSpan.FromSeconds(1)), VideoQualityPreset.Original, null, CancellationToken.None);

            Assert.Equal(destination, result);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
            Assert.Empty(runner.Arguments);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task TranscodeAsync_reports_fraction_from_ffmpeg_progress()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            var destination = Path.Combine(root.FullName, "output.mp4");
            await File.WriteAllTextAsync(source, "source");
            var runner = new StubProcessRunner(["out_time=00:00:01.000", "progress=continue"], args => File.WriteAllText(args[^1], "encoded"));
            var transcoder = new FfmpegVideoTranscoder(runner);
            var progress = new InlineProgress();

            await transcoder.TranscodeAsync(source, destination, new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)), VideoQualityPreset.P720, progress, CancellationToken.None);

            Assert.Contains(progress.Values, value => value.Fraction == 0.5);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task TranscodeAsync_removes_partial_output_when_process_fails()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            var destination = Path.Combine(root.FullName, "output.mp4");
            await File.WriteAllTextAsync(source, "source");
            var runner = new StubProcessRunner(onRun: args => File.WriteAllText(args[^1], "partial"), result: new ExternalProcessResult(1, "", "encode failed"));
            var transcoder = new FfmpegVideoTranscoder(runner);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                transcoder.TranscodeAsync(source, destination, new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)), VideoQualityPreset.P720, null, CancellationToken.None));

            Assert.Contains("encode failed", error.Message);
            Assert.False(File.Exists(destination));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task TranscodeAsync_removes_partial_output_when_cancelled()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            await File.WriteAllTextAsync(source, "source");
            var runner = new CancelAfterWriteRunner();
            var transcoder = new FfmpegVideoTranscoder(runner);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                transcoder.TranscodeAsync(source, Path.Combine(root.FullName, "output.mp4"), new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)), VideoQualityPreset.P720, null, CancellationToken.None));

            Assert.Equal([source], Directory.GetFiles(root.FullName));
        }
        finally { root.Delete(true); }
    }

    private sealed class CancelAfterWriteRunner : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken, IProgress<string>? standardOutputProgress = null, IProgress<string>? standardErrorProgress = null)
        {
            File.WriteAllText(arguments[^1], "partial");
            return Task.FromException<ExternalProcessResult>(new OperationCanceledException(cancellationToken));
        }
    }

    private sealed class StubProcessRunner(string[]? outputLines = null, Action<IReadOnlyList<string>>? onRun = null, ExternalProcessResult? result = null) : IExternalProcessRunner
    {
        public IReadOnlyList<string> Arguments { get; private set; } = [];
        public string? Executable { get; private set; }

        public Task<ExternalProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken, IProgress<string>? standardOutputProgress = null, IProgress<string>? standardErrorProgress = null)
        {
            Executable = executable;
            Arguments = arguments.ToArray();
            onRun?.Invoke(arguments);
            foreach (var line in outputLines ?? []) standardOutputProgress?.Report(line);
            return Task.FromResult(result ?? new ExternalProcessResult(0, string.Join(Environment.NewLine, outputLines ?? []), ""));
        }
    }

    private sealed class InlineProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Values { get; } = [];
        public void Report(DownloadProgress value) => Values.Add(value);
    }
}
