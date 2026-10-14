using System.Globalization;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class ConsoleWorkflow
{
    private readonly IVideoProbe _videoProbe;
    private readonly HttpDirectMediaDownloader _httpDownloader;
    private readonly DownloadCoordinator _coordinator;
    private readonly IDownloadHistoryRepository _history;
    private readonly string _destinationDirectory;
    private readonly IYouTubeVideoSource? _youTubeVideoSource;
    private readonly MediaToolchainStatus? _toolchain;

    public ConsoleWorkflow(
        IVideoProbe videoProbe,
        HttpDirectMediaDownloader httpDownloader,
        DownloadCoordinator coordinator,
        IDownloadHistoryRepository history,
        string? destinationDirectory = null,
        IYouTubeVideoSource? youTubeVideoSource = null,
        MediaToolchainStatus? toolchain = null)
    {
        _videoProbe = videoProbe ?? throw new ArgumentNullException(nameof(videoProbe));
        _httpDownloader = httpDownloader ?? throw new ArgumentNullException(nameof(httpDownloader));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _destinationDirectory = destinationDirectory ?? DefaultDownloadDirectory.GetPath();
        _youTubeVideoSource = youTubeVideoSource;
        _toolchain = toolchain;
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        string? stagingDirectory = null;
        Guid? jobId = null;
        Action<Guid, DownloadProgress> progressHandler = (_, progress) =>
        {
            if (progress.Fraction is double fraction)
                output.WriteLine($"Progresso: {fraction:P0}");
            else if (progress.TotalBytes is long total)
                output.WriteLine($"Transferidos: {progress.BytesTransferred:N0}/{total:N0} bytes");
            else
                output.WriteLine($"Transferidos: {progress.BytesTransferred:N0} bytes");
        };

        try
        {
            await output.WriteLineAsync("Scraps — processar mídia de fontes locais, diretas ou compatíveis.");
            await WriteToolchainWarningsAsync(output);
            await output.WriteLineAsync("Cole um link do YouTube, um caminho de arquivo ou uma URL direta autorizada:");
            var submittedSource = await input.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(submittedSource))
            {
                await output.WriteLineAsync("Nenhuma origem informada.");
                return;
            }

            string? youTubeVideoId = null;
            var mediaSource = submittedSource.Trim();
            if (YouTubeLinkParser.TryParse(mediaSource, out var parsedVideoId))
            {
                youTubeVideoId = parsedVideoId;
                await output.WriteLineAsync($"ID identificado: {youTubeVideoId}. O link será usado como origem do vídeo.");
            }
            else if (YouTubeLinkParser.IsYouTubeHost(mediaSource))
            {
                await output.WriteLineAsync("Link do YouTube inválido ou não suportado.");
                return;
            }

            if (string.IsNullOrWhiteSpace(mediaSource)
                || (youTubeVideoId is null
                    && (YouTubeLinkParser.TryParse(mediaSource, out _) || YouTubeLinkParser.IsYouTubeHost(mediaSource))))
            {
                await output.WriteLineAsync("Informe um arquivo local, uma URL direta de mídia ou um link de vídeo válido do YouTube.");
                return;
            }

            await output.WriteLineAsync("Confirme que você tem autorização para baixar e processar esta mídia (s/n). A confirmação não libera conteúdo restrito:");
            var authorization = await input.ReadLineAsync(cancellationToken);
            if (!IsAffirmative(authorization))
            {
                await output.WriteLineAsync("Operação cancelada: autorização não confirmada.");
                return;
            }

            var probePath = mediaSource;
            string? normalizedLocalPath = null;
            VideoInfo videoInfo;
            YouTubeVideoMetadata? youTubeMetadata = null;
            if (youTubeVideoId is not null)
            {
                if (_youTubeVideoSource is null)
                    throw new InvalidOperationException("O suporte a links do YouTube não está configurado.");
                if (!Uri.TryCreate(mediaSource, UriKind.Absolute, out var youTubeUri))
                    throw new InvalidOperationException("A URL do YouTube é inválida.");
                await output.WriteLineAsync("Consultando metadados públicos do YouTube...");
                youTubeMetadata = await _youTubeVideoSource.GetMetadataAsync(youTubeUri, cancellationToken);
                videoInfo = youTubeMetadata.VideoInfo;
            }
            else
            {
                if (Uri.TryCreate(mediaSource, UriKind.Absolute, out var mediaUri)
                    && (mediaUri.Scheme == Uri.UriSchemeHttp || mediaUri.Scheme == Uri.UriSchemeHttps))
                {
                    stagingDirectory = Path.Combine(Path.GetTempPath(), "Scraps", "staging", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(stagingDirectory);
                    await output.WriteLineAsync("Baixando a fonte direta para inspeção temporária...");
                    probePath = await _httpDownloader.DownloadAsync(mediaUri, stagingDirectory, cancellationToken: cancellationToken);
                }
                else
                {
                    normalizedLocalPath = Uri.TryCreate(mediaSource, UriKind.Absolute, out mediaUri) && mediaUri.IsFile
                        ? mediaUri.LocalPath
                        : Path.GetFullPath(mediaSource);
                    if (!File.Exists(normalizedLocalPath))
                    {
                        await output.WriteLineAsync($"Arquivo não encontrado: {normalizedLocalPath}");
                        return;
                    }
                    probePath = normalizedLocalPath;
                }
                videoInfo = await _videoProbe.ProbeAsync(probePath, cancellationToken);
            }

            var presets = GetAvailablePresets(videoInfo);
            var videoDescription = $"{videoInfo.Width}x{videoInfo.Height} — {videoInfo.Duration:c}";
            await output.WriteLineAsync(youTubeMetadata is null
                ? $"Vídeo: {videoDescription}"
                : $"Vídeo: {youTubeMetadata.Title} — {videoDescription}");
            await output.WriteLineAsync("Qualidade de saída:");
            for (var index = 0; index < presets.Count; index++)
                await output.WriteLineAsync($"{index + 1}. {QualityPresetSelector.GetDisplayName(presets[index])}");
            await output.WriteLineAsync("Escolha o número da opção (ou q para cancelar):");
            var choice = await input.ReadLineAsync(cancellationToken);
            if (string.Equals(choice?.Trim(), "q", StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("Operação cancelada.");
                return;
            }
            if (!int.TryParse(choice, NumberStyles.None, CultureInfo.InvariantCulture, out var selectedIndex)
                || selectedIndex < 1 || selectedIndex > presets.Count)
            {
                await output.WriteLineAsync("Opção de qualidade inválida.");
                return;
            }

            var request = new DownloadRequest(
                normalizedLocalPath ?? mediaSource,
                _destinationDirectory,
                presets[selectedIndex - 1],
                youTubeVideoId,
                preparedSourcePath: stagingDirectory is null ? null : probePath);
            _coordinator.ProgressChanged += progressHandler;
            var job = await _coordinator.EnqueueAsync(request, cancellationToken);
            jobId = job.Id;
            await output.WriteLineAsync($"Trabalho enfileirado: {job.Id}");

            var finished = await WaitForTerminalAsync(job.Id, cancellationToken);
            switch (finished.Status)
            {
                case DownloadStatus.Completed:
                    await output.WriteLineAsync($"Concluído: {finished.DestinationPath}");
                    break;
                case DownloadStatus.Failed:
                    await output.WriteLineAsync($"Falha: {finished.ErrorMessage}");
                    break;
                case DownloadStatus.Cancelled:
                    await output.WriteLineAsync("Operação cancelada.");
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (jobId is Guid id)
            {
                await _coordinator.CancelAsync(id, CancellationToken.None);
                using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await WaitForTerminalAsync(id, cleanupTimeout.Token); }
                catch (OperationCanceledException) { }
            }
            throw;
        }
        finally
        {
            _coordinator.ProgressChanged -= progressHandler;
            if (stagingDirectory is not null && Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    // Página oficial do projeto; ela lista as builds de Windows de terceiros (gyan.dev e BtbN).
    private const string FfmpegDownloadUrl = "https://ffmpeg.org/download.html";

    private async Task WriteToolchainWarningsAsync(TextWriter output)
    {
        if (_toolchain is null) return;
        if (!_toolchain.FfmpegAvailable)
            await output.WriteLineAsync($"Aviso: ffmpeg não encontrado. Vídeos do YouTube serão baixados em formato único, que pode ter qualidade menor que a fonte. Os presets de conversão (1080p, 720p, 480p, 360p) ficam indisponíveis até que o ffmpeg seja instalado e adicionado ao PATH. Download: {FfmpegDownloadUrl}");
        if (!_toolchain.FfprobeAvailable)
            await output.WriteLineAsync($"Aviso: ffprobe não encontrado. A análise de vídeos falhará até que ele seja instalado e adicionado ao PATH. Ele vem no mesmo pacote do ffmpeg: {FfmpegDownloadUrl}");
    }

    // Sem ffmpeg não há conversão, então só a opção Original permanece disponível.
    private IReadOnlyList<VideoQualityPreset> GetAvailablePresets(VideoInfo videoInfo) =>
        _toolchain is { FfmpegAvailable: false }
            ? [VideoQualityPreset.Original]
            : QualityPresetSelector.GetAvailable(videoInfo);

    private async Task<DownloadJob> WaitForTerminalAsync(Guid jobId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var job = await _history.GetAsync(jobId, cancellationToken)
                ?? throw new InvalidOperationException("The queued job could not be found in history.");
            if (job.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
                return job;
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static bool IsAffirmative(string? answer) =>
        answer?.Trim().ToLowerInvariant() is "s" or "sim" or "y" or "yes";
}
