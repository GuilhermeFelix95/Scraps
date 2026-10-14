using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

// Monta o grafo de serviços a partir das ferramentas resolvidas. Usado pela CLI e pela interface.
public static class ScrapsComposition
{
    public static async Task<ScrapsServices> CreateAsync(
        string baseDirectory,
        string? ffmpegOverride,
        string? ytDlpOverride,
        string? javaScriptRuntime,
        CancellationToken cancellationToken)
    {
        var allocator = new DestinationPathAllocator();
        var httpDownloader = new HttpDirectMediaDownloader(allocator);
        var processRunner = new SystemExternalProcessRunner();
        var ffmpeg = ToolLocator.Resolve(ffmpegOverride, baseDirectory, "ffmpeg", "ffmpeg");
        var ffprobe = ToolLocator.Resolve(null, baseDirectory, "ffmpeg", "ffprobe");
        var ytDlp = ToolLocator.Resolve(ytDlpOverride, baseDirectory, "yt-dlp", "yt-dlp");

        var toolchain = await new MediaToolchainChecker(processRunner, ffmpeg, ffprobe).CheckAsync(cancellationToken);
        // Sem ffmpeg disponível o yt-dlp não recebe localização nem tenta mesclar faixas.
        var youTube = new YtDlpYouTubeVideoSource(processRunner, ytDlp, javaScriptRuntime,
            ffmpegExecutable: toolchain.FfmpegAvailable ? ffmpeg : null);
        var videoProbe = new FfprobeVideoProbe(processRunner, ffprobe);
        var transcoder = new FfmpegVideoTranscoder(processRunner, ffmpeg);
        var history = new SqliteDownloadHistoryRepository(DatabaseInitializer.GetDefaultDatabasePath());
        var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), httpDownloader, videoProbe, transcoder, allocator, youTube);

        return new ScrapsServices
        {
            Coordinator = new DownloadCoordinator(history, executor),
            History = history,
            Import = new MediaImportService(youTube, videoProbe, httpDownloader, toolchain),
            Toolchain = toolchain,
            ProcessRunner = processRunner,
            YtDlpExecutable = ytDlp,
            HttpDownloader = httpDownloader
        };
    }
}
