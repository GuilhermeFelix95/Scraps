namespace Scraps.Application.Downloads;

public interface IMediaTranscoder
{
    Task<string> TranscodeAsync(
        string sourcePath,
        string destinationPath,
        VideoInfo sourceInfo,
        VideoQualityPreset preset,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken);
}
