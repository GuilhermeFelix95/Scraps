namespace Scraps.Application.Downloads;

// Resolve a origem até ter metadados, presets disponíveis e, quando preciso, um arquivo inspecionado.
// Não enfileira nem interage com o usuário: a interface decide quando chamar cada passo.
public sealed class MediaImportService(
    IYouTubeVideoSource? youTubeVideoSource,
    IVideoProbe videoProbe,
    IDirectMediaDownloader directDownloader,
    MediaToolchainStatus? toolchain)
{
    public async Task<PreparedMedia> PrepareAsync(MediaSourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Kind switch
        {
            MediaSourceKind.YouTube => await PrepareYouTubeAsync(request, cancellationToken),
            MediaSourceKind.DirectUrl => await PrepareDirectAsync(request, cancellationToken),
            _ => await PrepareLocalAsync(request, cancellationToken)
        };
    }

    private async Task<PreparedMedia> PrepareYouTubeAsync(MediaSourceRequest request, CancellationToken cancellationToken)
    {
        if (youTubeVideoSource is null)
            throw new InvalidOperationException("O suporte a links do YouTube não está configurado.");
        if (!Uri.TryCreate(request.Source, UriKind.Absolute, out var youTubeUri))
            throw new InvalidOperationException("A URL do YouTube é inválida.");

        var metadata = await youTubeVideoSource.GetMetadataAsync(youTubeUri, cancellationToken);
        return new PreparedMedia
        {
            Source = request.Source,
            YouTubeVideoId = request.YouTubeVideoId,
            Title = metadata.Title,
            VideoInfo = metadata.VideoInfo,
            Presets = AvailablePresets(metadata.VideoInfo)
        };
    }

    // URL direta é baixada uma única vez para inspeção; o arquivo baixado segue para a fila sem nova transferência.
    private async Task<PreparedMedia> PrepareDirectAsync(MediaSourceRequest request, CancellationToken cancellationToken)
    {
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "Scraps", "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var stagedPath = await directDownloader.DownloadAsync(new Uri(request.Source), stagingDirectory, cancellationToken: cancellationToken);
            var videoInfo = await videoProbe.ProbeAsync(stagedPath, cancellationToken);
            return new PreparedMedia
            {
                Source = request.Source,
                VideoInfo = videoInfo,
                Presets = AvailablePresets(videoInfo),
                PreparedSourcePath = stagedPath,
                StagingDirectory = stagingDirectory
            };
        }
        catch
        {
            if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, recursive: true);
            throw;
        }
    }

    private async Task<PreparedMedia> PrepareLocalAsync(MediaSourceRequest request, CancellationToken cancellationToken)
    {
        var localPath = Uri.TryCreate(request.Source, UriKind.Absolute, out var fileUri) && fileUri.IsFile
            ? fileUri.LocalPath
            : Path.GetFullPath(request.Source);
        if (!File.Exists(localPath))
            throw new MediaSourceException($"Arquivo não encontrado: {localPath}");

        var videoInfo = await videoProbe.ProbeAsync(localPath, cancellationToken);
        return new PreparedMedia
        {
            Source = localPath,
            VideoInfo = videoInfo,
            Presets = AvailablePresets(videoInfo)
        };
    }

    // Sem ffmpeg não há conversão, então só a opção Original permanece disponível.
    private IReadOnlyList<VideoQualityPreset> AvailablePresets(VideoInfo videoInfo) =>
        toolchain is { FfmpegAvailable: false }
            ? [VideoQualityPreset.Original]
            : QualityPresetSelector.GetAvailable(videoInfo);
}
