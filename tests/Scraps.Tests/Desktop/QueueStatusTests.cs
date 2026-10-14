using Scraps.Application.Downloads;
using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Desktop;

public sealed class QueueStatusTests
{
    [Fact]
    public async Task Item_moves_from_queued_to_downloading_when_the_job_starts()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var executor = new HoldingExecutor();
            await using var coordinator = new DownloadCoordinator(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")), executor);
            await coordinator.StartAsync();
            using var queue = new QueueViewModel(coordinator, post: action => action());
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/s.mp4", root.FullName));
            queue.Track(job);
            var item = queue.Jobs.Single();

            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntil(() => item.Status == DownloadStatus.Downloading);

            executor.Release.TrySetResult(Path.Combine(root.FullName, "s.mp4"));
            await WaitUntil(() => item.Status == DownloadStatus.Completed);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Track_after_the_job_started_shows_its_current_status()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var executor = new HoldingExecutor();
            await using var coordinator = new DownloadCoordinator(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")), executor);
            await coordinator.StartAsync();
            using var queue = new QueueViewModel(coordinator, post: action => action());
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/t.mp4", root.FullName));
            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // O job ainda é o snapshot de Queued, que a UI recebe logo após EnqueueAsync.
            queue.Track(job);

            var item = queue.Jobs.Single();
            await WaitUntil(() => item.Status == DownloadStatus.Downloading);
            executor.Release.TrySetResult(Path.Combine(root.FullName, "t.mp4"));
            await WaitUntil(() => item.Status == DownloadStatus.Completed);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task A_job_that_cannot_be_read_shows_an_error_instead_of_a_stale_status()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var history = new ReadFailingRepository(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")));
            await using var coordinator = new DownloadCoordinator(history, new HoldingExecutor());
            await coordinator.StartAsync();
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/u.mp4", root.FullName));
            history.FailReads = true;

            using var queue = new QueueViewModel(coordinator, post: action => action());
            queue.Track(job);

            var item = queue.Jobs.Single();
            await WaitUntil(() => item.FollowError is not null);
            Assert.Contains("Não foi possível acompanhar", item.FollowError);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private sealed class ReadFailingRepository(IDownloadHistoryRepository inner) : IDownloadHistoryRepository
    {
        public bool FailReads { get; set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);

        public Task SaveAsync(DownloadJob job, CancellationToken cancellationToken = default) => inner.SaveAsync(job, cancellationToken);

        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            FailReads ? throw new IOException("disco indisponível") : inner.GetAsync(id, cancellationToken);

        public Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);

        public Task<DownloadHistoryPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default) =>
            inner.GetPageAsync(cursor, pageSize, cancellationToken);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(50);
        Assert.True(condition(), "A condição esperada não ocorreu a tempo.");
    }

    private sealed class HoldingExecutor : IDownloadExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return Release.Task.WaitAsync(cancellationToken);
        }
    }
}
