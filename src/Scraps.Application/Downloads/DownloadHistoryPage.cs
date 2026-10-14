namespace Scraps.Application.Downloads;

// Uma página do histórico, do mais recente para o mais antigo. NextCursor é nulo na última página.
public sealed record DownloadHistoryPage(IReadOnlyList<DownloadJob> Items, string? NextCursor)
{
    public const int MaxPageSize = 200;
}
