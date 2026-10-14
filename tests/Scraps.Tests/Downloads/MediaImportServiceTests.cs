using Scraps.Application.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class MediaImportServiceTests
{
    [Fact]
    public void Parse_rejects_unsupported_youtube_page()
    {
        var exception = Assert.Throws<MediaSourceException>(() => MediaSourceParser.Parse("https://youtu.be/abcdefghijk/extra"));

        Assert.Contains("Link do YouTube inválido", exception.Message);
    }

    [Fact]
    public void Parse_classifies_youtube_direct_and_local_sources()
    {
        var youtube = MediaSourceParser.Parse("https://www.youtube.com/watch?v=abcdefghijk");
        var direct = MediaSourceParser.Parse("https://cdn.example/video.mp4");
        var local = MediaSourceParser.Parse(@"C:\videos\clip.mp4");

        Assert.Equal(MediaSourceKind.YouTube, youtube.Kind);
        Assert.Equal("abcdefghijk", youtube.YouTubeVideoId);
        Assert.Equal(MediaSourceKind.DirectUrl, direct.Kind);
        Assert.Equal(MediaSourceKind.LocalFile, local.Kind);
    }

    [Fact]
    public async Task PrepareAsync_reports_missing_local_file()
    {
        var service = new MediaImportService(null, new StubVideoProbe(), new ThrowingDirectDownloader(), toolchain: null);
        var missing = Path.Combine(Path.GetTempPath(), $"scraps-missing-{Guid.NewGuid():N}.mp4");

        var exception = await Assert.ThrowsAsync<MediaSourceException>(() =>
            service.PrepareAsync(MediaSourceParser.Parse(missing), CancellationToken.None));

        Assert.Contains("Arquivo não encontrado", exception.Message);
    }

    [Fact]
    public async Task PrepareAsync_offers_only_original_when_ffmpeg_is_missing()
    {
        var videoInfo = new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2));
        var youTube = new StubYouTubeVideoSource(new YouTubeVideoMetadata("abcdefghijk", "Sample", videoInfo));
        var toolchain = new MediaToolchainStatus(FfmpegAvailable: false, FfprobeAvailable: true);
        var service = new MediaImportService(youTube, new StubVideoProbe(), new ThrowingDirectDownloader(), toolchain);

        var media = await service.PrepareAsync(MediaSourceParser.Parse("https://youtu.be/abcdefghijk"), CancellationToken.None);

        Assert.Single(media.Presets);
        Assert.Equal(VideoQualityPreset.Original, media.Presets[0]);
        Assert.Equal("Sample", media.Title);
    }

    private sealed class StubVideoProbe : IVideoProbe
    {
        public Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
    }

    private sealed class StubYouTubeVideoSource(YouTubeVideoMetadata metadata) : IYouTubeVideoSource
    {
        public Task<YouTubeVideoMetadata> GetMetadataAsync(Uri source, CancellationToken cancellationToken) => Task.FromResult(metadata);

        public Task<string> DownloadAsync(Uri source, string expectedVideoId, string stagingDirectory,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Não deve baixar na preparação.");
    }

    private sealed class ThrowingDirectDownloader : IDirectMediaDownloader
    {
        public Task<string> DownloadAsync(Uri source, string destinationDirectory, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Não deve baixar neste teste.");
    }
}
