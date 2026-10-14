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
