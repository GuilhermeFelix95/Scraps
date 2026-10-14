using System.Net;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class MediaDownloadExecutorTests
{
    [Fact]
    public async Task Execute_imports_local_file_when_source_is_a_path()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            var destination = Path.Combine(root.FullName, "library");
            await File.WriteAllTextAsync(source, "local media");
            using var http = new HttpDirectMediaDownloader(new DestinationPathAllocator());
            var executor = new MediaDownloadExecutor(new LocalFileImporter(new DestinationPathAllocator()), http);

            var output = await executor.ExecuteAsync(
                DownloadJob.Create(source, destination),
                progress: null,
                cancellationToken: CancellationToken.None);

            Assert.Equal("local media", await File.ReadAllTextAsync(output));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Execute_downloads_http_source()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var handler = new ResponseHandler();
            using var http = new HttpDirectMediaDownloader(
                new DestinationPathAllocator(), handler,
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));
            var executor = new MediaDownloadExecutor(new LocalFileImporter(new DestinationPathAllocator()), http);

            var output = await executor.ExecuteAsync(
                DownloadJob.Create("https://media.example/video.mp4", root.FullName),
                progress: null,
                cancellationToken: CancellationToken.None);

            Assert.Equal(new byte[] { 4, 5, 6 }, await File.ReadAllBytesAsync(output));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Execute_transcodes_local_source_to_selected_mp4_quality()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mov");
            var destination = Path.Combine(root.FullName, "output");
            await File.WriteAllTextAsync(source, "source-bytes");
            var allocator = new DestinationPathAllocator();
            var probe = new StubVideoProbe(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
            var transcoder = new StubTranscoder();
            using var http = new HttpDirectMediaDownloader(allocator);
            var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), http, probe, transcoder, allocator);

            var output = await executor.ExecuteAsync(
                DownloadJob.Create(source, destination, VideoQualityPreset.P720),
                progress: null,
                cancellationToken: CancellationToken.None);

            Assert.Equal(Path.Combine(destination, "source.mp4"), output);
            Assert.Equal("transcoded", await File.ReadAllTextAsync(output));
            Assert.Equal(VideoQualityPreset.P720, transcoder.LastPreset);
            Assert.Equal(source, probe.LastPath);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Execute_uses_prepared_direct_url_source_without_downloading_twice()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var preparedDirectory = Path.Combine(root.FullName, "staging");
            Directory.CreateDirectory(preparedDirectory);
            var preparedSource = Path.Combine(preparedDirectory, "clip.mp4");
            await File.WriteAllTextAsync(preparedSource, "prepared");
            var allocator = new DestinationPathAllocator();
            var probe = new StubVideoProbe(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
            var transcoder = new StubTranscoder();
            using var http = new HttpDirectMediaDownloader(allocator, new FailingHandler(),
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));
            var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), http, probe, transcoder, allocator);
            var job = DownloadJob.Create("https://media.example/clip.mp4", Path.Combine(root.FullName, "output"), VideoQualityPreset.P720,
                preparedSourcePath: preparedSource);

            var output = await executor.ExecuteAsync(job, null, CancellationToken.None);

            Assert.Equal(Path.Combine(root.FullName, "output", "clip.mp4"), output);
            Assert.Equal(preparedSource, probe.LastPath);
        }
        finally { root.Delete(true); }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The prepared source must be used without a second HTTP request.");
    }

    [Fact]
    public async Task Execute_downloads_youtube_video_to_staging_and_transcodes_it()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var allocator = new DestinationPathAllocator();
            var youtubeSource = new StubYouTubeVideoSource();
            var probe = new StubVideoProbe(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
            var transcoder = new StubTranscoder();
            using var http = new HttpDirectMediaDownloader(allocator, new FailingHandler());
            var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), http, probe, transcoder, allocator, youtubeSource);
            var job = DownloadJob.Create("https://youtu.be/abcdefghijk", Path.Combine(root.FullName, "output"),
                VideoQualityPreset.P720, "abcdefghijk");

            var output = await executor.ExecuteAsync(job, null, CancellationToken.None);

            Assert.Equal(Path.Combine(root.FullName, "output", "abcdefghijk.mp4"), output);
            Assert.Equal("https://youtu.be/abcdefghijk", youtubeSource.LastDownloadSource?.AbsoluteUri);
            Assert.Equal(youtubeSource.LastDownloadedPath, probe.LastPath);
            Assert.False(File.Exists(youtubeSource.LastDownloadedPath));
            Assert.Equal(VideoQualityPreset.P720, transcoder.LastPreset);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Execute_cleans_youtube_staging_after_transcode_cancellation()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var allocator = new DestinationPathAllocator();
            var youtubeSource = new StubYouTubeVideoSource();
            var probe = new StubVideoProbe(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
            using var http = new HttpDirectMediaDownloader(allocator, new FailingHandler());
            var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), http, probe,
                new CancellingTranscoder(), allocator, youtubeSource);
            var job = DownloadJob.Create("https://youtu.be/abcdefghijk", Path.Combine(root.FullName, "output"),
                VideoQualityPreset.P720, "abcdefghijk");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(job, null, CancellationToken.None));

            Assert.NotNull(youtubeSource.LastDownloadedPath);
            Assert.False(Directory.Exists(Path.GetDirectoryName(youtubeSource.LastDownloadedPath)));
        }
        finally { root.Delete(true); }
    }

    private sealed class StubYouTubeVideoSource : IYouTubeVideoSource
    {
        public Uri? LastDownloadSource { get; private set; }
        public string? LastDownloadedPath { get; private set; }

        public Task<YouTubeVideoMetadata> GetMetadataAsync(Uri source, CancellationToken cancellationToken) =>
            Task.FromResult(new YouTubeVideoMetadata("abcdefghijk", "Sample", new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2))));

        public async Task<string> DownloadAsync(Uri source, string expectedVideoId, string stagingDirectory,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            LastDownloadSource = source;
            Directory.CreateDirectory(stagingDirectory);
            LastDownloadedPath = Path.Combine(stagingDirectory, $"{expectedVideoId}.mkv");
            await File.WriteAllTextAsync(LastDownloadedPath, "downloaded", cancellationToken);
            return LastDownloadedPath;
        }
    }

    private sealed class CancellingTranscoder : IMediaTranscoder
    {
        public Task<string> TranscodeAsync(string sourcePath, string destinationPath, VideoInfo sourceInfo,
            VideoQualityPreset preset, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromException<string>(new OperationCanceledException(cancellationToken));
    }

    private sealed class StubVideoProbe(VideoInfo result) : IVideoProbe
    {
        public string? LastPath { get; private set; }
        public Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            LastPath = path;
            return Task.FromResult(result);
        }
    }

    private sealed class StubTranscoder : IMediaTranscoder
    {
        public VideoQualityPreset LastPreset { get; private set; }
        public async Task<string> TranscodeAsync(string sourcePath, string destinationPath, VideoInfo sourceInfo, VideoQualityPreset preset, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            LastPreset = preset;
            await File.WriteAllTextAsync(destinationPath, "transcoded", cancellationToken);
            return destinationPath;
        }
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 4, 5, 6 })
            });
    }
}
