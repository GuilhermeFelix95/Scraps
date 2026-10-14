namespace Scraps.Application.Downloads;

public interface IDownloadHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DownloadJob job, CancellationToken cancellationToken = default);
    Task<DownloadJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken cancellationToken = default);
    // cursor nulo começa pela página mais recente; um cursor só vale se veio de GetPageAsync.
    Task<DownloadHistoryPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default);
}
