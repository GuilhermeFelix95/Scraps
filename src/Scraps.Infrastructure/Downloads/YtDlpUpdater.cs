using System.ComponentModel;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public enum ToolUpdateStatus
{
    Skipped,
    Succeeded,
    Failed
}

public sealed record ToolUpdateResult(ToolUpdateStatus Status, string Message);

// Atualiza só o yt-dlp embutido em third-party/. Instalações do PATH ou apontadas por SCRAPS_YTDLP são de quem as gerencia.
public sealed class YtDlpUpdater(IExternalProcessRunner processRunner, string bundledDirectory)
{
    // Limite para não travar a inicialização quando a rede está lenta ou fora do ar.
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(30);

    public async Task<ToolUpdateResult> UpdateAsync(string executable, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (!IsBundled(executable))
            return new ToolUpdateResult(ToolUpdateStatus.Skipped, "yt-dlp externo: atualização automática desativada.");

        using var timeout = new CancellationTokenSource(UpdateTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            // -U é a atualização nativa do yt-dlp, que confere o SHA2-256SUMS oficial antes de substituir o binário.
            var result = await processRunner.RunAsync(executable, ["-U"], linked.Token);
            return result.ExitCode == 0
                ? new ToolUpdateResult(ToolUpdateStatus.Succeeded, LastLine(result.StandardOutput))
                : new ToolUpdateResult(ToolUpdateStatus.Failed, LastLine(result.StandardError, result.StandardOutput));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ToolUpdateResult(ToolUpdateStatus.Failed, $"tempo esgotado após {UpdateTimeout.TotalSeconds:0} s");
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return new ToolUpdateResult(ToolUpdateStatus.Failed, exception.Message);
        }
    }

    private bool IsBundled(string executable)
    {
        var folder = Path.GetFullPath(bundledDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(executable).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    private static string LastLine(params string[] streams)
    {
        var text = streams.FirstOrDefault(stream => !string.IsNullOrWhiteSpace(stream)) ?? string.Empty;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? "sem detalhes" : lines[^1];
    }
}
