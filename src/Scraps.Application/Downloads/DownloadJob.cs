namespace Scraps.Application.Downloads;

public sealed record DownloadJob
{
    private DownloadJob(Guid id, string source, string destinationPath, DownloadStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt, string? errorMessage, VideoQualityPreset preset, string? youTubeVideoId, string? preparedSourcePath)
    {
        Id = id;
        Source = source;
        DestinationPath = destinationPath;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ErrorMessage = errorMessage;
        Preset = preset;
        YouTubeVideoId = youTubeVideoId;
        PreparedSourcePath = preparedSourcePath;
    }

    public Guid Id { get; }
    public string Source { get; }
    public string DestinationPath { get; }
    public DownloadStatus Status { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public string? ErrorMessage { get; }
    public VideoQualityPreset Preset { get; }
    public string? YouTubeVideoId { get; }
    public string? PreparedSourcePath { get; }

    public static DownloadJob Create(string source, string destinationPath, VideoQualityPreset preset = VideoQualityPreset.Original, string? youTubeVideoId = null, string? preparedSourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!Enum.IsDefined(preset)) throw new ArgumentOutOfRangeException(nameof(preset));
        if (youTubeVideoId is not null && !YouTubeLinkParser.IsVideoId(youTubeVideoId))
            throw new ArgumentException("The YouTube video ID is invalid.", nameof(youTubeVideoId));
        if (preparedSourcePath is not null) ArgumentException.ThrowIfNullOrWhiteSpace(preparedSourcePath);

        var now = DateTimeOffset.UtcNow;
        return new DownloadJob(Guid.NewGuid(), source, destinationPath, DownloadStatus.Queued, now, now, null, preset, youTubeVideoId, preparedSourcePath);
    }

    public static DownloadJob Rehydrate(Guid id, string source, string destinationPath, DownloadStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt, string? errorMessage, VideoQualityPreset preset = VideoQualityPreset.Original, string? youTubeVideoId = null, string? preparedSourcePath = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Job id cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (!Enum.IsDefined(preset)) throw new ArgumentOutOfRangeException(nameof(preset));
        if (youTubeVideoId is not null && !YouTubeLinkParser.IsVideoId(youTubeVideoId))
            throw new ArgumentException("The YouTube video ID is invalid.", nameof(youTubeVideoId));
        if (preparedSourcePath is not null) ArgumentException.ThrowIfNullOrWhiteSpace(preparedSourcePath);

        return new DownloadJob(id, source, destinationPath, status, createdAt, updatedAt, errorMessage, preset, youTubeVideoId, preparedSourcePath);
    }

    public DownloadJob TransitionTo(DownloadStatus next, string? errorMessage = null, string? destinationPath = null)
    {
        var allowed = Status switch
        {
            DownloadStatus.Queued => next is DownloadStatus.Downloading or DownloadStatus.Failed or DownloadStatus.Cancelled,
            DownloadStatus.Downloading => next is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"Cannot transition a {Status} job to {next}.");
        }

        if (destinationPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        }
        if (next == DownloadStatus.Failed && string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "The download failed.";
        }

        var preparedSourcePath = next is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled
            ? null
            : PreparedSourcePath;
        return new DownloadJob(Id, Source, destinationPath ?? DestinationPath, next, CreatedAt, DateTimeOffset.UtcNow, errorMessage, Preset, YouTubeVideoId, preparedSourcePath);
    }
}
