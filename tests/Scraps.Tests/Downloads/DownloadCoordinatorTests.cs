using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class DownloadCoordinatorTests
{
    [Fact]
    public async Task Enqueued_job_runs_and_persists_completed_destination()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new TestExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/video.mp4", root.FullName));

            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            executor.Release.TrySetResult(Path.Combine(root.FullName, "video.mp4"));
            await WaitForStatus(repository, job.Id, DownloadStatus.Completed);
            await coordinator.StopAsync();

            var saved = await repository.GetAsync(job.Id);
            Assert.Equal(Path.Combine(root.FullName, "video.mp4"), saved!.DestinationPath);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Cancel_queued_job_prevents_executor_from_running_it()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new TestExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var first = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/first.mp4", root.FullName));
            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/second.mp4", root.FullName));

            Assert.True(await coordinator.CancelAsync(second.Id));
            executor.Release.TrySetResult(Path.Combine(root.FullName, "first.mp4"));
            await WaitForStatus(repository, first.Id, DownloadStatus.Completed);
            await coordinator.StopAsync();

            Assert.Equal(1, executor.ExecutionCount);
            Assert.Equal(DownloadStatus.Cancelled, (await repository.GetAsync(second.Id))!.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Start_marks_queued_and_downloading_jobs_failed_instead_of_resuming()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            await repository.InitializeAsync();
            var queued = DownloadJob.Create("https://media.example/queued.mp4", root.FullName);
            var downloading = DownloadJob.Create("https://media.example/active.mp4", root.FullName)
                .TransitionTo(DownloadStatus.Downloading);
            await repository.SaveAsync(queued);
            await repository.SaveAsync(downloading);
            var executor = new TestExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);

            await coordinator.StartAsync();
            var all = await repository.GetAllAsync();
            await coordinator.StopAsync();

            Assert.All(all, job => Assert.Equal(DownloadStatus.Failed, job.Status));
            Assert.Equal(0, executor.ExecutionCount);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Cancel_active_job_cancels_executor_and_persists_status()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new TestExecutor(blockUntilCancelled: true);
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/video.mp4", root.FullName));
            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(await coordinator.CancelAsync(job.Id));
            await WaitForStatus(repository, job.Id, DownloadStatus.Cancelled);
            await coordinator.StopAsync();
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static async Task WaitForStatus(IDownloadHistoryRepository repository, Guid id, DownloadStatus status)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if ((await repository.GetAsync(id))?.Status == status) return;
            await Task.Delay(20);
        }

        Assert.Fail($"Job {id} did not reach status {status}.");
    }

    private sealed class TestExecutor(bool blockUntilCancelled = false) : IDownloadExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ExecutionCount { get; private set; }

        public async Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            Started.TrySetResult();
            progress?.Report(new DownloadProgress(0, 1));
            if (blockUntilCancelled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
