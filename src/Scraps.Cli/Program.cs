using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var allocator = new DestinationPathAllocator();
using var httpDownloader = new HttpDirectMediaDownloader(allocator);
var processRunner = new SystemExternalProcessRunner();
var ffmpegExecutable = ToolLocator.Resolve(Environment.GetEnvironmentVariable("SCRAPS_FFMPEG"), AppContext.BaseDirectory, "ffmpeg", "ffmpeg");
var ffprobeExecutable = ToolLocator.Resolve(null, AppContext.BaseDirectory, "ffmpeg", "ffprobe");
var ytDlpExecutable = ToolLocator.Resolve(Environment.GetEnvironmentVariable("SCRAPS_YTDLP"), AppContext.BaseDirectory, "yt-dlp", "yt-dlp");
var toolchain = await new MediaToolchainChecker(processRunner, ffmpegExecutable, ffprobeExecutable).CheckAsync(shutdown.Token);
var ytDlpJavaScriptRuntime = Environment.GetEnvironmentVariable("SCRAPS_YTDLP_JS_RUNTIME");
var youTubeVideoSource = new YtDlpYouTubeVideoSource(processRunner,
    ytDlpExecutable,
    string.IsNullOrWhiteSpace(ytDlpJavaScriptRuntime) ? null : ytDlpJavaScriptRuntime,
    mergeAvailable: toolchain.FfmpegAvailable);
var videoProbe = new FfprobeVideoProbe(processRunner, ffprobeExecutable);
var transcoder = new FfmpegVideoTranscoder(processRunner, ffmpegExecutable);
var history = new SqliteDownloadHistoryRepository(DatabaseInitializer.GetDefaultDatabasePath());
var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), httpDownloader, videoProbe, transcoder, allocator, youTubeVideoSource);
await using var coordinator = new DownloadCoordinator(history, executor);

try
{
    await coordinator.StartAsync(shutdown.Token);
    var update = await new YtDlpUpdater(processRunner, Path.Combine(AppContext.BaseDirectory, "third-party"))
        .UpdateAsync(ytDlpExecutable, shutdown.Token);
    if (update.Status == ToolUpdateStatus.Failed)
        Console.WriteLine($"Aviso: não foi possível atualizar o yt-dlp ({update.Message}). Usando a versão instalada.");
    else if (update.Status == ToolUpdateStatus.Succeeded)
        Console.WriteLine(update.Message);
    var workflow = new ConsoleWorkflow(videoProbe, httpDownloader, coordinator, history, youTubeVideoSource: youTubeVideoSource, toolchain: toolchain);
    await workflow.RunAsync(Console.In, Console.Out, shutdown.Token);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    Console.WriteLine("Operação interrompida.");
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Erro: {exception.Message}");
    return 1;
}
finally
{
    shutdown.Dispose();
}
