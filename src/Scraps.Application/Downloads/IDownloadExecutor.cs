namespace Scraps.Application.Downloads;

public interface IDownloadExecutor
{
    Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
