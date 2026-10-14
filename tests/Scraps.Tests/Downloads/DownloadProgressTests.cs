using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class DownloadProgressTests
{
    [Fact]
    public void Fraction_can_report_media_conversion_progress_without_transfer_byte_counts()
    {
        var progress = new DownloadProgress(0, null, 0.5);

        Assert.Equal(0.5, progress.Fraction);
    }

    [Fact]
    public async Task Coordinator_publishes_transferred_bytes_for_ui_consumers()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new ReportingExecutor();
            await using var coordinator = new DownloadCoordinator(repository, executor);
            var observed = new TaskCompletionSource<DownloadProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.ProgressChanged += (_, progress) => observed.TrySetResult(progress);
            await coordinator.StartAsync();
            await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/video.mp4", root.FullName));

            var progress = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await coordinator.StopAsync();

            Assert.Equal(64, progress.BytesTransferred);
            Assert.Equal(128, progress.TotalBytes);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private sealed class ReportingExecutor : IDownloadExecutor
    {
        public async Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            progress?.Report(new DownloadProgress(64, 128));
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return job.DestinationPath;
        }
    }
}
