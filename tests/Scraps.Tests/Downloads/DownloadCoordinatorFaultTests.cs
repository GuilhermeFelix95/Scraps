using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class DownloadCoordinatorFaultTests
{
    [Fact]
    public async Task A_failing_observer_is_reported_and_the_job_still_completes()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            await using var coordinator = new DownloadCoordinator(
                new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")),
                new ImmediateExecutor(Path.Combine(root.FullName, "observed.mp4")));
            var faults = new List<(Guid JobId, Exception Exception)>();
            coordinator.WorkerFaulted += (jobId, exception) => faults.Add((jobId, exception));
            coordinator.JobStarted += _ => throw new InvalidOperationException("observador quebrado");
            await coordinator.StartAsync();

            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/observed.mp4", root.FullName));
            var finished = await coordinator.WaitForFinishedAsync(job.Id).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(DownloadStatus.Completed, finished.Status);
            var fault = Assert.Single(faults);
            Assert.Equal(job.Id, fault.JobId);
            Assert.Equal("observador quebrado", fault.Exception.Message);
        }
        finally
        {
            root.Delete(true);
        }
    }

    // Executor que conclui na hora, para que o teste não dependa de tempo.
    private sealed class ImmediateExecutor(string destinationPath) : IDownloadExecutor
    {
        public Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(destinationPath);
    }
}
