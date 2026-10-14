using Scraps.Application.Downloads;

namespace Scraps.Tests.Downloads;

public class DownloadJobTests
{
    [Fact]
    public void Create_starts_queued()
    {
        var job = DownloadJob.Create("https://cdn.example/video.mp4", "C:/Videos/video.mp4");

        Assert.Equal(DownloadStatus.Queued, job.Status);
        Assert.NotEqual(Guid.Empty, job.Id);
    }

    [Fact]
    public void Transition_to_completed_requires_downloading_first()
    {
        var job = DownloadJob.Create("https://cdn.example/video.mp4", "C:/Videos/video.mp4");

        Assert.Throws<InvalidOperationException>(() => job.TransitionTo(DownloadStatus.Completed));
    }

    [Fact]
    public void Downloading_job_can_complete()
    {
        var queued = DownloadJob.Create("https://cdn.example/video.mp4", "C:/Videos/video.mp4");

        var downloading = queued.TransitionTo(DownloadStatus.Downloading);
        var completed = downloading.TransitionTo(DownloadStatus.Completed);

        Assert.Equal(DownloadStatus.Downloading, downloading.Status);
        Assert.Equal(DownloadStatus.Completed, completed.Status);
        Assert.Equal(DownloadStatus.Queued, queued.Status);
    }

    [Fact]
    public void Completion_can_record_resolved_destination_path()
    {
        var queued = DownloadJob.Create("https://cdn.example/video.mp4", "C:/Videos");
        var downloading = queued.TransitionTo(DownloadStatus.Downloading);

        var completed = downloading.TransitionTo(DownloadStatus.Completed, destinationPath: "C:/Videos/video (1).mp4");

        Assert.Equal("C:/Videos/video (1).mp4", completed.DestinationPath);
    }

    [Fact]
    public void Terminal_job_does_not_retain_ephemeral_prepared_source_path()
    {
        var preparedSourcePath = "C:/Temp/Scraps/staging/source.mp4";
        var queued = DownloadJob.Create("https://cdn.example/video.mp4", "C:/Videos", VideoQualityPreset.P720, preparedSourcePath: preparedSourcePath);
        var downloading = queued.TransitionTo(DownloadStatus.Downloading);

        var completed = downloading.TransitionTo(DownloadStatus.Completed);

        Assert.Equal(preparedSourcePath, downloading.PreparedSourcePath);
        Assert.Null(completed.PreparedSourcePath);
    }

    [Fact]
    public void Terminal_job_cannot_transition_again()
    {
        var queued = DownloadJob.Create("https://cdn.example/video.mp4", "C:/Videos/video.mp4");
        var completed = queued
            .TransitionTo(DownloadStatus.Downloading)
            .TransitionTo(DownloadStatus.Completed);

        Assert.Throws<InvalidOperationException>(() => completed.TransitionTo(DownloadStatus.Failed));
    }
}
