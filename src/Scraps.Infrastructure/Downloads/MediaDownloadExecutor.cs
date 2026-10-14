using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class MediaDownloadExecutor(
    LocalFileImporter localFileImporter,
    HttpDirectMediaDownloader httpDownloader,
    IVideoProbe? videoProbe = null,
    IMediaTranscoder? mediaTranscoder = null,
    DestinationPathAllocator? pathAllocator = null,
    IYouTubeVideoSource? youTubeVideoSource = null) : IDownloadExecutor
{
    public async Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var source = job.PreparedSourcePath ?? job.Source;
        string? stagingDirectory = null;
        try
        {
            if (job.YouTubeVideoId is not null)
            {
                if (youTubeVideoSource is null)
                    throw new InvalidOperationException("The YouTube video source is not configured.");
                if (!Uri.TryCreate(job.Source, UriKind.Absolute, out var youTubeUri))
                    throw new InvalidOperationException("The YouTube job does not contain a valid URL.");
                stagingDirectory = CreateStagingDirectory();
                source = await youTubeVideoSource.DownloadAsync(
                    youTubeUri, job.YouTubeVideoId, stagingDirectory, progress, cancellationToken);
            }
            else if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
                     && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                if (job.Preset == VideoQualityPreset.Original)
                    return await httpDownloader.DownloadAsync(uri, job.DestinationPath, progress, cancellationToken);

                stagingDirectory = CreateStagingDirectory();
                source = await httpDownloader.DownloadAsync(uri, stagingDirectory, progress, cancellationToken);
            }
            else if (Uri.TryCreate(source, UriKind.Absolute, out var fileUri) && fileUri.IsFile)
            {
                source = fileUri.LocalPath;
            }

            if (job.Preset == VideoQualityPreset.Original)
                return await localFileImporter.ImportAsync(source, job.DestinationPath, progress, cancellationToken);

            if (videoProbe is null || mediaTranscoder is null)
                throw new InvalidOperationException("Video conversion services are not configured.");

            var sourceInfo = await videoProbe.ProbeAsync(source, cancellationToken);
            if (!QualityPresetSelector.GetAvailable(sourceInfo).Contains(job.Preset))
                throw new InvalidOperationException("The selected quality is not available for this source video.");

            var allocator = pathAllocator ?? new DestinationPathAllocator();
            var suggestedName = $"{Path.GetFileNameWithoutExtension(source)}.mp4";
            var destinationPath = await allocator.AllocateAsync(job.DestinationPath, suggestedName, cancellationToken);
            return await mediaTranscoder.TranscodeAsync(source, destinationPath, sourceInfo, job.Preset, progress, cancellationToken);
        }
        finally
        {
            if (stagingDirectory is not null && Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private static string CreateStagingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Scraps", "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
