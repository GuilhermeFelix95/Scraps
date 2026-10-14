using System.Globalization;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

// Adaptador de terminal: lê as respostas, chama o caso de uso e escreve o resultado. Não contém regra de negócio.
public sealed class ConsoleWorkflow
{
    // Página oficial do projeto; ela lista as builds de Windows de terceiros (gyan.dev e BtbN).
    private static readonly TimeSpan CancellationCleanupTimeout = TimeSpan.FromSeconds(30);

    private readonly MediaImportService _import;
    private readonly DownloadCoordinator _coordinator;
    private readonly string _destinationDirectory;
    private readonly MediaToolchainStatus? _toolchain;

    public ConsoleWorkflow(
        MediaImportService import,
        DownloadCoordinator coordinator,
        string? destinationDirectory = null,
        MediaToolchainStatus? toolchain = null)
    {
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _destinationDirectory = destinationDirectory ?? DefaultDownloadDirectory.GetPath();
        _toolchain = toolchain;
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        PreparedMedia? media = null;
        Guid? jobId = null;
        Action<Guid, DownloadProgress> progressHandler = (_, progress) => WriteProgress(output, progress);

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

            MediaSourceRequest sourceRequest;
            try
            {
                sourceRequest = MediaSourceParser.Parse(submittedSource);
            }
            catch (MediaSourceException exception)
            {
                await output.WriteLineAsync(exception.Message);
                return;
            }
            if (sourceRequest.Kind == MediaSourceKind.YouTube)
                await output.WriteLineAsync($"ID identificado: {sourceRequest.YouTubeVideoId}. O link será usado como origem do vídeo.");

            await output.WriteLineAsync("Confirme que você tem autorização para baixar e processar esta mídia (s/n). A confirmação não libera conteúdo restrito:");
            var authorization = await input.ReadLineAsync(cancellationToken);
            if (!IsAffirmative(authorization))
            {
                await output.WriteLineAsync("Operação cancelada: autorização não confirmada.");
                return;
            }

            if (sourceRequest.Kind == MediaSourceKind.YouTube)
                await output.WriteLineAsync("Consultando metadados públicos do YouTube...");
            else if (sourceRequest.Kind == MediaSourceKind.DirectUrl)
                await output.WriteLineAsync("Baixando a fonte direta para inspeção temporária...");

            try
            {
                media = await _import.PrepareAsync(sourceRequest, cancellationToken);
            }
            catch (MediaSourceException exception)
            {
                await output.WriteLineAsync(exception.Message);
                return;
            }

            var videoDescription = $"{media.VideoInfo.Width}x{media.VideoInfo.Height} — {media.VideoInfo.Duration:c}";
            await output.WriteLineAsync(media.Title is null
                ? $"Vídeo: {videoDescription}"
                : $"Vídeo: {media.Title} — {videoDescription}");
            await output.WriteLineAsync("Qualidade de saída:");
            for (var index = 0; index < media.Presets.Count; index++)
                await output.WriteLineAsync($"{index + 1}. {QualityPresetSelector.GetDisplayName(media.Presets[index])}");
            await output.WriteLineAsync("Escolha o número da opção (ou q para cancelar):");
            var choice = await input.ReadLineAsync(cancellationToken);
            if (string.Equals(choice?.Trim(), "q", StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("Operação cancelada.");
                return;
            }
            if (!int.TryParse(choice, NumberStyles.None, CultureInfo.InvariantCulture, out var selectedIndex)
                || selectedIndex < 1 || selectedIndex > media.Presets.Count)
            {
                await output.WriteLineAsync("Opção de qualidade inválida.");
                return;
            }

            var downloadRequest = media.CreateRequest(media.Presets[selectedIndex - 1], _destinationDirectory);
            _coordinator.ProgressChanged += progressHandler;
            var job = await _coordinator.EnqueueAsync(downloadRequest, cancellationToken);
            jobId = job.Id;
            await output.WriteLineAsync($"Trabalho enfileirado: {job.Id}");

            var finished = await _coordinator.WaitForFinishedAsync(job.Id, cancellationToken);
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
                await CancelAndWaitAsync(id, output);
            throw;
        }
        finally
        {
            _coordinator.ProgressChanged -= progressHandler;
            media?.Dispose();
        }
    }

    private async Task CancelAndWaitAsync(Guid jobId, TextWriter output)
    {
        await _coordinator.CancelAsync(jobId, CancellationToken.None);
        using var cleanupTimeout = new CancellationTokenSource(CancellationCleanupTimeout);
        try
        {
            await _coordinator.WaitForFinishedAsync(jobId, cleanupTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            await output.WriteLineAsync($"Aviso: o cancelamento ainda não terminou após {CancellationCleanupTimeout.TotalSeconds:0} s.");
        }
    }

    private static void WriteProgress(TextWriter output, DownloadProgress progress)
    {
        if (progress.Fraction is double fraction)
            output.WriteLine($"Progresso: {fraction:P0}");
        else if (progress.TotalBytes is long total)
            output.WriteLine($"Transferidos: {progress.BytesTransferred:N0}/{total:N0} bytes");
        else
            output.WriteLine($"Transferidos: {progress.BytesTransferred:N0} bytes");
    }

    private async Task WriteToolchainWarningsAsync(TextWriter output)
    {
        if (_toolchain is null) return;
        foreach (var warning in ToolchainWarnings.From(_toolchain))
            await output.WriteLineAsync($"Aviso: {warning}");
    }

    private static bool IsAffirmative(string? answer) =>
        answer?.Trim().ToLowerInvariant() is "s" or "sim" or "y" or "yes";
}
