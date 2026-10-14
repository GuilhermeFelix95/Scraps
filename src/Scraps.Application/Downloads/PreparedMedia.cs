namespace Scraps.Application.Downloads;

// Estado de uma origem pronta para enfileirar. Dispor remove a pasta temporária de inspeção,
// então o chamador precisa mantê-lo vivo até o job terminar.
public sealed class PreparedMedia : IDisposable
{
    public required string Source { get; init; }
    public string? YouTubeVideoId { get; init; }
    public string? Title { get; init; }
    public required VideoInfo VideoInfo { get; init; }
    public required IReadOnlyList<VideoQualityPreset> Presets { get; init; }
    public string? PreparedSourcePath { get; init; }
    public string? StagingDirectory { get; init; }

    public DownloadRequest CreateRequest(VideoQualityPreset preset, string destinationDirectory)
    {
        if (!Presets.Contains(preset))
            throw new MediaSourceException("Opção de qualidade inválida.");
        return new DownloadRequest(Source, destinationDirectory, preset, YouTubeVideoId, PreparedSourcePath);
    }

    public void Dispose()
    {
        if (StagingDirectory is not null && Directory.Exists(StagingDirectory))
            Directory.Delete(StagingDirectory, recursive: true);
    }
}
