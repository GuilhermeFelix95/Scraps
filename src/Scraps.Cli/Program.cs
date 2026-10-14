using Scraps.Infrastructure.Downloads;

var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

try
{
    await using var services = await ScrapsComposition.CreateAsync(
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("SCRAPS_FFMPEG"),
        Environment.GetEnvironmentVariable("SCRAPS_YTDLP"),
        Environment.GetEnvironmentVariable("SCRAPS_YTDLP_JS_RUNTIME"),
        shutdown.Token);
    await services.Coordinator.StartAsync(shutdown.Token);
    var update = await services.Updater.UpdateAsync(services.YtDlpExecutable, shutdown.Token);
    if (update.Status == ToolUpdateStatus.Failed)
        Console.WriteLine($"Aviso: não foi possível atualizar o yt-dlp ({update.Message}). Usando a versão instalada.");
    else if (update.Status == ToolUpdateStatus.Succeeded)
        Console.WriteLine(update.Message);
    var workflow = new ConsoleWorkflow(services.Import, services.Coordinator, toolchain: services.Toolchain);
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
