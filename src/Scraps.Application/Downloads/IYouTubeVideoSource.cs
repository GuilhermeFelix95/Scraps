namespace Scraps.Application.Downloads;

public interface IYouTubeVideoSource
{
    Task<YouTubeVideoMetadata> GetMetadataAsync(Uri source, CancellationToken cancellationToken);

    Task<string> DownloadAsync(
        Uri source,
        string expectedVideoId,
        string stagingDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken);
}
