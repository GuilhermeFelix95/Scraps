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
var ytDlpExecutable = Environment.GetEnvironmentVariable("SCRAPS_YTDLP");
var ytDlpJavaScriptRuntime = Environment.GetEnvironmentVariable("SCRAPS_YTDLP_JS_RUNTIME");
var youTubeVideoSource = new YtDlpYouTubeVideoSource(processRunner,
    string.IsNullOrWhiteSpace(ytDlpExecutable) ? "yt-dlp" : ytDlpExecutable,
    string.IsNullOrWhiteSpace(ytDlpJavaScriptRuntime) ? null : ytDlpJavaScriptRuntime);
var videoProbe = new FfprobeVideoProbe(processRunner);
var transcoder = new FfmpegVideoTranscoder(processRunner);
var history = new SqliteDownloadHistoryRepository(DatabaseInitializer.GetDefaultDatabasePath());
var executor = new MediaDownloadExecutor(new LocalFileImporter(allocator), httpDownloader, videoProbe, transcoder, allocator, youTubeVideoSource);
await using var coordinator = new DownloadCoordinator(history, executor);

try
{
    await coordinator.StartAsync(shutdown.Token);
    var workflow = new ConsoleWorkflow(videoProbe, httpDownloader, coordinator, history, youTubeVideoSource: youTubeVideoSource);
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
