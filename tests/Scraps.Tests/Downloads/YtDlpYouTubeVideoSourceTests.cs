using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class YtDlpYouTubeVideoSourceTests
{
    [Fact]
    public async Task GetMetadataAsync_parses_title_duration_and_maximum_video_resolution()
    {
        const string json = """
            {"id":"abcdefghijk","title":"A sample video","duration":12.5,"formats":[{"width":1920,"height":1080,"vcodec":"avc1","acodec":"none"},{"width":1280,"height":720,"vcodec":"avc1","acodec":"none"},{"width":0,"height":null,"vcodec":"none","acodec":"mp4a"}]}
            """;
        var runner = new StubProcessRunner(new ExternalProcessResult(0, json, string.Empty));
        var source = new YtDlpYouTubeVideoSource(runner);

        var metadata = await source.GetMetadataAsync(new Uri("https://www.youtube.com/watch?v=abcdefghijk"), CancellationToken.None);

        Assert.Equal("abcdefghijk", metadata.VideoId);
        Assert.Equal("A sample video", metadata.Title);
        Assert.Equal(1920, metadata.VideoInfo.Width);
        Assert.Equal(1080, metadata.VideoInfo.Height);
        Assert.Equal(TimeSpan.FromSeconds(12.5), metadata.VideoInfo.Duration);
        Assert.Contains("--ignore-config", runner.Arguments!.ToArray());
        Assert.Contains("--no-playlist", runner.Arguments!.ToArray());
        Assert.Contains("--xff", runner.Arguments!.ToArray());
        Assert.Contains("never", runner.Arguments!.ToArray());
        Assert.Contains("--dump-single-json", runner.Arguments!.ToArray());
        Assert.Contains("--skip-download", runner.Arguments!.ToArray());
    }

    [Fact]
    public async Task GetMetadataAsync_configures_explicit_javascript_runtime()
    {
        const string json = """
            {"id":"abcdefghijk","title":"A sample video","duration":12.5,"formats":[{"width":1920,"height":1080,"vcodec":"avc1"}]}
            """;
        var runner = new StubProcessRunner(new ExternalProcessResult(0, json, string.Empty));
        var source = new YtDlpYouTubeVideoSource(runner, javaScriptRuntime: "node");

        await source.GetMetadataAsync(new Uri("https://youtu.be/abcdefghijk"), CancellationToken.None);

        var arguments = runner.Arguments!.ToArray();
        Assert.Equal(["--no-js-runtimes", "--js-runtimes", "node"], arguments.Take(3));
    }

    [Fact]
    public async Task DownloadAsync_uses_single_video_safe_output_and_reports_progress()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var runner = new StubProcessRunner(new ExternalProcessResult(0, string.Empty, string.Empty),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    var outputPath = arguments[outputIndex + 1].Replace("%(ext)s", "mkv", StringComparison.Ordinal);
                    File.WriteAllText(outputPath, "video-bytes");
                },
                stderrLine: "SCRAPS_PROGRESS:25.0%");
            var source = new YtDlpYouTubeVideoSource(runner);
            var progress = new RecordingProgress();

            var path = await source.DownloadAsync(
                new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", root.FullName, progress, CancellationToken.None);

            Assert.Equal(Path.Combine(root.FullName, "abcdefghijk.mkv"), path);
            Assert.Contains("--no-playlist", runner.Arguments!.ToArray());
            Assert.Contains("--ignore-config", runner.Arguments!.ToArray());
            Assert.Contains("--xff", runner.Arguments!.ToArray());
            Assert.Contains("never", runner.Arguments!.ToArray());
            Assert.Contains("--format", runner.Arguments!.ToArray());
            Assert.Contains("bestvideo+bestaudio/best", runner.Arguments!.ToArray());
            Assert.Contains("--merge-output-format", runner.Arguments!.ToArray());
            Assert.Equal(0.25, progress.Fractions[0], precision: 2);
            Assert.Equal(1, progress.Fractions[^1]);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task DownloadAsync_reports_process_output_and_staging_contents_when_no_video_is_found()
    {
        var root = Directory.CreateTempSubdirectory();
        var stagingDirectory = Path.Combine(root.FullName, "staging");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var runner = new StubProcessRunner(new ExternalProcessResult(0, "download skipped", "warning detail"),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    var outputDirectory = Path.GetDirectoryName(arguments[outputIndex + 1])!;
                    File.WriteAllText(Path.Combine(outputDirectory, "diagnostic.txt"), "sidecar");
                });
            var source = new YtDlpYouTubeVideoSource(runner);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => source.DownloadAsync(
                new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", stagingDirectory, null, CancellationToken.None));

            Assert.Contains("código 0", exception.Message);
            Assert.Contains("diagnostic.txt", exception.Message);
            Assert.Contains("download skipped", exception.Message);
            Assert.Contains("warning detail", exception.Message);
            Assert.Contains(stagingDirectory, exception.Message);
            Assert.DoesNotContain("--no-warnings", runner.Arguments!.ToArray());
            Assert.False(Directory.Exists(stagingDirectory));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task DownloadAsync_removes_partial_output_when_extractor_fails()
    {
        var root = Directory.CreateTempSubdirectory();
        var stagingDirectory = Path.Combine(root.FullName, "staging");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var runner = new StubProcessRunner(new ExternalProcessResult(1, string.Empty, "network error"),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    var partialPath = arguments[outputIndex + 1].Replace("%(ext)s", "mkv.part", StringComparison.Ordinal);
                    File.WriteAllText(partialPath, "partial");
                });
            var source = new YtDlpYouTubeVideoSource(runner);

            await Assert.ThrowsAsync<InvalidOperationException>(() => source.DownloadAsync(
                new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", stagingDirectory, null, CancellationToken.None));

            Assert.False(Directory.Exists(stagingDirectory));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task DownloadAsync_uses_single_file_format_when_ffmpeg_is_unavailable()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var runner = new StubProcessRunner(new ExternalProcessResult(0, string.Empty, string.Empty),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    File.WriteAllText(arguments[outputIndex + 1].Replace("%(ext)s", "mp4", StringComparison.Ordinal), "video-bytes");
                });
            var source = new YtDlpYouTubeVideoSource(runner, ffmpegExecutable: null);

            await source.DownloadAsync(new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", root.FullName, null, CancellationToken.None);

            var arguments = runner.Arguments!.ToArray();
            Assert.Contains("best[ext=mp4]/best", arguments);
            Assert.DoesNotContain("bestvideo+bestaudio/best", arguments);
            Assert.DoesNotContain("--merge-output-format", arguments);
        }
        finally { root.Delete(true); }
    }

    // Reproduz o erro real: ffmpeg embutido em third-party/ não estava no PATH, e o yt-dlp baixou as faixas sem mesclar.
    [Fact]
    public async Task DownloadAsync_passes_bundled_ffmpeg_location_to_yt_dlp()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ffmpegPath = Path.Combine(root.FullName, "third-party", "ffmpeg", "ffmpeg.exe");
            var runner = new StubProcessRunner(new ExternalProcessResult(0, string.Empty, string.Empty),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    File.WriteAllText(arguments[outputIndex + 1].Replace("%(ext)s", "mkv", StringComparison.Ordinal), "video-bytes");
                });
            var source = new YtDlpYouTubeVideoSource(runner, ffmpegExecutable: ffmpegPath);

            await source.DownloadAsync(new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", root.FullName, null, CancellationToken.None);

            var arguments = runner.Arguments!.ToArray();
            var locationIndex = Array.IndexOf(arguments, "--ffmpeg-location");
            Assert.True(locationIndex >= 0, "yt-dlp precisa receber a localização do ffmpeg embutido.");
            Assert.Equal(ffmpegPath, arguments[locationIndex + 1]);
            Assert.Contains("bestvideo+bestaudio/best", arguments);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task DownloadAsync_omits_ffmpeg_location_when_ffmpeg_comes_from_path()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var runner = new StubProcessRunner(new ExternalProcessResult(0, string.Empty, string.Empty),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    File.WriteAllText(arguments[outputIndex + 1].Replace("%(ext)s", "mkv", StringComparison.Ordinal), "video-bytes");
                });
            var source = new YtDlpYouTubeVideoSource(runner, ffmpegExecutable: "ffmpeg");

            await source.DownloadAsync(new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", root.FullName, null, CancellationToken.None);

            var arguments = runner.Arguments!.ToArray();
            Assert.DoesNotContain("--ffmpeg-location", arguments);
            Assert.Contains("bestvideo+bestaudio/best", arguments);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task DownloadAsync_explains_unmerged_streams_and_keeps_stderr_visible()
    {
        var root = Directory.CreateTempSubdirectory();
        var stagingDirectory = Path.Combine(root.FullName, "staging");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var progressLines = string.Concat(Enumerable.Repeat($"SCRAPS_PROGRESS:  5.0%{Environment.NewLine}", 300));
            var runner = new StubProcessRunner(new ExternalProcessResult(0, progressLines, "WARNING: ffmpeg not found. The formats won't be merged."),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    var outputDirectory = Path.GetDirectoryName(arguments[outputIndex + 1])!;
                    File.WriteAllText(Path.Combine(outputDirectory, "abcdefghijk.f399.mp4"), "video-only");
                    File.WriteAllText(Path.Combine(outputDirectory, "abcdefghijk.f251-2.webm"), "audio-only");
                });
            var source = new YtDlpYouTubeVideoSource(runner);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => source.DownloadAsync(
                new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", stagingDirectory, null, CancellationToken.None));

            Assert.Contains("não foram mescladas", exception.Message);
            Assert.Contains("ffmpeg", exception.Message);
            Assert.Contains("WARNING: ffmpeg not found", exception.Message);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task DownloadAsync_points_to_the_configured_ffmpeg_path_when_streams_are_not_merged()
    {
        var root = Directory.CreateTempSubdirectory();
        var stagingDirectory = Path.Combine(root.FullName, "staging");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var ffmpegPath = Path.Combine(root.FullName, "tools", "ffmpeg.exe");
            var runner = new StubProcessRunner(new ExternalProcessResult(0, string.Empty, string.Empty),
                onRun: arguments =>
                {
                    var outputIndex = Array.IndexOf(arguments.ToArray(), "--output");
                    var outputDirectory = Path.GetDirectoryName(arguments[outputIndex + 1])!;
                    File.WriteAllText(Path.Combine(outputDirectory, "abcdefghijk.f399.mp4"), "video-only");
                    File.WriteAllText(Path.Combine(outputDirectory, "abcdefghijk.f251-2.webm"), "audio-only");
                });
            var source = new YtDlpYouTubeVideoSource(runner, ffmpegExecutable: ffmpegPath);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => source.DownloadAsync(
                new Uri("https://youtu.be/abcdefghijk"), "abcdefghijk", stagingDirectory, null, CancellationToken.None));

            Assert.Contains(ffmpegPath, exception.Message);
            Assert.DoesNotContain("acessível pelo PATH", exception.Message);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task GetMetadataAsync_rejects_non_youtube_urls_before_starting_process()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(0, "{}", string.Empty));
        var source = new YtDlpYouTubeVideoSource(runner);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.GetMetadataAsync(new Uri("https://example.com/video"), CancellationToken.None));

        Assert.Null(runner.Arguments);
    }

    [Fact]
    public async Task GetMetadataAsync_explains_javascript_runtime_dependency()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(1, string.Empty,
            "No supported JavaScript runtime could be found"));
        var source = new YtDlpYouTubeVideoSource(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.GetMetadataAsync(new Uri("https://youtu.be/abcdefghijk"), CancellationToken.None));

        Assert.Contains("SCRAPS_YTDLP_JS_RUNTIME", exception.Message);
        Assert.Contains("yt-dlp-ejs", exception.Message);
    }

    [Fact]
    public async Task GetMetadataAsync_reports_extractor_failure()
    {
        var runner = new StubProcessRunner(new ExternalProcessResult(1, string.Empty, "video unavailable"));
        var source = new YtDlpYouTubeVideoSource(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.GetMetadataAsync(new Uri("https://youtu.be/abcdefghijk"), CancellationToken.None));

        Assert.Contains("video unavailable", exception.Message);
    }

    private sealed class RecordingProgress : IProgress<DownloadProgress>
    {
        public List<double> Fractions { get; } = [];
        public void Report(DownloadProgress value)
        {
            if (value.ProgressFraction is double fraction) Fractions.Add(fraction);
        }
    }

    private sealed class StubProcessRunner(
        ExternalProcessResult result,
        Action<IReadOnlyList<string>>? onRun = null,
        string? stderrLine = null) : IExternalProcessRunner
    {
        public IReadOnlyList<string>? Arguments { get; private set; }

        public Task<ExternalProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<string>? standardOutputProgress = null,
            IProgress<string>? standardErrorProgress = null)
        {
            Arguments = arguments.ToArray();
            onRun?.Invoke(arguments);
            if (stderrLine is not null) standardErrorProgress?.Report(stderrLine);
            return Task.FromResult(result);
        }
    }
}
