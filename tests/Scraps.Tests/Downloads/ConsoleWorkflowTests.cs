using System.Net;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class ConsoleWorkflowTests
{
    [Fact]
    public async Task RunAsync_accepts_youtube_link_as_source_and_enqueues_after_authorization()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new RecordingExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var videoInfo = new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2));
            var probe = new StubVideoProbe(videoInfo);
            var youtubeSource = new StubYouTubeVideoSource(new YouTubeVideoMetadata("abcdefghijk", "Sample video", videoInfo));
            var handler = new CountingHandler();
            using var http = new HttpDirectMediaDownloader(new DestinationPathAllocator(), handler,
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));
            var workflow = new ConsoleWorkflow(probe, http, coordinator, repository, Path.Combine(root.FullName, "Downloads", "Scraps"), youtubeSource);
            var output = new StringWriter();
            var input = new StringReader("https://youtu.be/abcdefghijk\ns\n3\n");

            await workflow.RunAsync(input, output, CancellationToken.None);

            Assert.NotNull(executor.LastJob);
            Assert.Equal("https://youtu.be/abcdefghijk", executor.LastJob.Source);
            Assert.Equal("abcdefghijk", executor.LastJob.YouTubeVideoId);
            Assert.Equal(VideoQualityPreset.P720, executor.LastJob.Preset);
            Assert.Equal("https://youtu.be/abcdefghijk", youtubeSource.LastMetadataSource?.AbsoluteUri);
            Assert.Equal(0, handler.RequestCount);
            Assert.Contains("Sample video", output.ToString());
            Assert.Contains("720p", output.ToString());
            Assert.Contains("Concluído", output.ToString());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task RunAsync_accepts_youtube_watch_url_as_source_after_authorization()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new RecordingExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var videoInfo = new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2));
            var probe = new StubVideoProbe(videoInfo);
            var youtubeSource = new StubYouTubeVideoSource(new YouTubeVideoMetadata("abcdefghijk", "Watch video", videoInfo));
            using var http = new HttpDirectMediaDownloader(new DestinationPathAllocator());
            var workflow = new ConsoleWorkflow(probe, http, coordinator, repository, Path.Combine(root.FullName, "Downloads"), youtubeSource);
            var output = new StringWriter();
            var input = new StringReader("https://www.youtube.com/watch?v=abcdefghijk\ns\n3\n");

            await workflow.RunAsync(input, output, CancellationToken.None);

            Assert.NotNull(executor.LastJob);
            Assert.Equal("https://www.youtube.com/watch?v=abcdefghijk", executor.LastJob.Source);
            Assert.Equal("abcdefghijk", executor.LastJob.YouTubeVideoId);
            Assert.Equal(VideoQualityPreset.P720, executor.LastJob.Preset);
            Assert.Equal("https://www.youtube.com/watch?v=abcdefghijk", youtubeSource.LastMetadataSource?.AbsoluteUri);
            Assert.Contains("Watch video", output.ToString());
            Assert.Contains("Concluído", output.ToString());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task RunAsync_rejects_unsupported_youtube_page_without_requesting_it_as_media()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new RecordingExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var probe = new StubVideoProbe(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
            var handler = new CountingHandler();
            using var http = new HttpDirectMediaDownloader(new DestinationPathAllocator(), handler,
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));
            var workflow = new ConsoleWorkflow(probe, http, coordinator, repository, Path.Combine(root.FullName, "Downloads"));
            var output = new StringWriter();
            var input = new StringReader("https://youtu.be/abcdefghijk/extra\ns\n1\n");

            await workflow.RunAsync(input, output, CancellationToken.None);

            Assert.Equal(0, handler.RequestCount);
            Assert.Null(executor.LastJob);
            Assert.Contains("Link do YouTube inválido", output.ToString());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task RunAsync_stages_authorized_direct_url_once_and_cleans_temporary_source()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new RecordingExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var probe = new StubVideoProbe(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
            var handler = new CountingHandler();
            using var http = new HttpDirectMediaDownloader(new DestinationPathAllocator(), handler,
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));
            var workflow = new ConsoleWorkflow(probe, http, coordinator, repository, Path.Combine(root.FullName, "Downloads"));
            var output = new StringWriter();
            var input = new StringReader("https://cdn.example/video.mp4\ns\n1\n");

            await workflow.RunAsync(input, output, CancellationToken.None);

            Assert.NotNull(executor.LastJob);
            Assert.Equal("https://cdn.example/video.mp4", executor.LastJob.Source);
            Assert.NotNull(executor.LastJob.PreparedSourcePath);
            Assert.False(File.Exists(executor.LastJob.PreparedSourcePath));
            Assert.Equal(1, handler.RequestCount);
            Assert.Contains("Concluído", output.ToString());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task RunAsync_offers_only_original_and_warns_when_ffmpeg_is_missing()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new RecordingExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var videoInfo = new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2));
            var youtubeSource = new StubYouTubeVideoSource(new YouTubeVideoMetadata("abcdefghijk", "Sample video", videoInfo));
            using var http = new HttpDirectMediaDownloader(new DestinationPathAllocator());
            var toolchain = new MediaToolchainStatus(FfmpegAvailable: false, FfprobeAvailable: true);
            var workflow = new ConsoleWorkflow(new StubVideoProbe(videoInfo), http, coordinator, repository,
                Path.Combine(root.FullName, "Downloads"), youtubeSource, toolchain: toolchain);
            var output = new StringWriter();
            var input = new StringReader("https://youtu.be/abcdefghijk\ns\n1\n");

            await workflow.RunAsync(input, output, CancellationToken.None);

            Assert.Contains("Aviso: ffmpeg não encontrado", output.ToString());
            Assert.DoesNotContain("2. 720p", output.ToString());
            Assert.NotNull(executor.LastJob);
            Assert.Equal(VideoQualityPreset.Original, executor.LastJob.Preset);
        }
        finally { root.Delete(true); }
    }

    private sealed class StubVideoProbe(VideoInfo result) : IVideoProbe
    {
        public Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class StubYouTubeVideoSource(YouTubeVideoMetadata metadata) : IYouTubeVideoSource
    {
        public Uri? LastMetadataSource { get; private set; }

        public Task<YouTubeVideoMetadata> GetMetadataAsync(Uri source, CancellationToken cancellationToken)
        {
            LastMetadataSource = source;
            return Task.FromResult(metadata);
        }

        public async Task<string> DownloadAsync(Uri source, string expectedVideoId, string stagingDirectory,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(stagingDirectory);
            var output = Path.Combine(stagingDirectory, $"{expectedVideoId}.mkv");
            await File.WriteAllTextAsync(output, "video", cancellationToken);
            return output;
        }
    }

    private sealed class RecordingExecutor : IDownloadExecutor
    {
        public DownloadJob? LastJob { get; private set; }
        public Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            LastJob = job;
            return Task.FromResult(Path.Combine(job.DestinationPath, "result.mp4"));
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        }
    }
}
