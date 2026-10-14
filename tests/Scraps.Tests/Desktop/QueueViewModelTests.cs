using Scraps.Application.Downloads;
using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Desktop;

public sealed class QueueViewModelTests
{
    [Fact]
    public async Task Track_adds_item_with_job_identity_and_source()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var executor = new ManualExecutor();
            await using var coordinator = await StartAsync(root, executor);
            using var queue = new QueueViewModel(coordinator, post: action => action());
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/a.mp4", root.FullName));

            queue.Track(job);

            var item = Assert.Single(queue.Jobs);
            Assert.Equal(job.Id, item.Id);
            Assert.Equal("https://media.example/a.mp4", item.Label);
            executor.Release.TrySetResult(Path.Combine(root.FullName, "a.mp4"));
            await WaitUntil(() => item.Status == DownloadStatus.Completed);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Progress_reported_by_executor_is_applied_to_item()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var executor = new ManualExecutor();
            await using var coordinator = await StartAsync(root, executor);
            using var queue = new QueueViewModel(coordinator, post: action => action());
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/b.mp4", root.FullName));
            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queue.Track(job);
            var item = queue.Jobs.Single();

            executor.Progress!.Report(new DownloadProgress(50, 100, 0.5));

            await WaitUntil(() => item.Progress == 0.5);
            executor.Release.TrySetResult(Path.Combine(root.FullName, "b.mp4"));
            await WaitUntil(() => item.Status == DownloadStatus.Completed);
            Assert.False(item.IsActive);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Cancel_command_marks_running_item_as_cancelled()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var executor = new ManualExecutor();
            await using var coordinator = await StartAsync(root, executor);
            using var queue = new QueueViewModel(coordinator, post: action => action());
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/c.mp4", root.FullName));
            queue.Track(job);
            var item = queue.Jobs.Single();
            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await item.CancelCommand.ExecuteAsync(null);
            await WaitUntil(() => item.Status == DownloadStatus.Cancelled);

            Assert.False(item.IsActive);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Track_after_job_finished_shows_final_status()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var executor = new ManualExecutor();
            await using var coordinator = await StartAsync(root, executor);
            using var queue = new QueueViewModel(coordinator, post: action => action());
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/d.mp4", root.FullName));
            executor.Release.TrySetResult(Path.Combine(root.FullName, "d.mp4"));
            await coordinator.WaitForFinishedAsync(job.Id);

            queue.Track(job);

            var item = queue.Jobs.Single();
            await WaitUntil(() => item.Status == DownloadStatus.Completed);
            Assert.Equal(1, item.Progress);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static async Task<DownloadCoordinator> StartAsync(DirectoryInfo root, IDownloadExecutor executor)
    {
        var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
        var coordinator = new DownloadCoordinator(repository, executor);
        await coordinator.StartAsync();
        return coordinator;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(50);
        Assert.True(condition(), "A condição esperada não ocorreu a tempo.");
    }

    // Executor controlado pelo teste: informa quando começou, expõe o relatório de progresso e termina quando liberado.
    private sealed class ManualExecutor : IDownloadExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IProgress<DownloadProgress>? Progress { get; private set; }

        public Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Progress = progress;
            Started.TrySetResult();
            return Release.Task.WaitAsync(cancellationToken);
        }
    }
}
