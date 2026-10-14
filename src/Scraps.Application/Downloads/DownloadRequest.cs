namespace Scraps.Application.Downloads;

public sealed record DownloadRequest
{
    public DownloadRequest(string source, string destinationDirectory, VideoQualityPreset preset = VideoQualityPreset.Original, string? youTubeVideoId = null, string? preparedSourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!Enum.IsDefined(preset)) throw new ArgumentOutOfRangeException(nameof(preset));
        if (youTubeVideoId is not null && !YouTubeLinkParser.IsVideoId(youTubeVideoId))
            throw new ArgumentException("The YouTube video ID is invalid.", nameof(youTubeVideoId));
        if (preparedSourcePath is not null) ArgumentException.ThrowIfNullOrWhiteSpace(preparedSourcePath);
        Source = source;
        DestinationDirectory = destinationDirectory;
        Preset = preset;
        YouTubeVideoId = youTubeVideoId;
        PreparedSourcePath = preparedSourcePath;
    }

    public string Source { get; }
    public string DestinationDirectory { get; }
    public VideoQualityPreset Preset { get; }
    public string? YouTubeVideoId { get; }
    public string? PreparedSourcePath { get; }
}
