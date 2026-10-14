namespace Scraps.Application.Downloads;

public interface IDirectMediaDownloader
{
    Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
