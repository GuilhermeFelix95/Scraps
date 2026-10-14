using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

// Conjunto de serviços já montados. Dispor encerra a fila e libera o cliente HTTP.
public sealed class ScrapsServices : IAsyncDisposable
{
    public required DownloadCoordinator Coordinator { get; init; }
    public required MediaImportService Import { get; init; }
    public required MediaToolchainStatus Toolchain { get; init; }
    public required IExternalProcessRunner ProcessRunner { get; init; }
    public required string YtDlpExecutable { get; init; }
    public required HttpDirectMediaDownloader HttpDownloader { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Coordinator.DisposeAsync();
        HttpDownloader.Dispose();
    }
}
