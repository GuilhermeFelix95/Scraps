using Microsoft.Data.Sqlite;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class SqliteDownloadHistoryRepositoryTests
{
    [Fact]
    public async Task Save_and_get_preserves_job_fields()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            await repository.InitializeAsync();
            var job = DownloadJob.Create("https://cdn.example/video.mp4", Path.Combine(root.FullName, "video.mp4"))
                .TransitionTo(DownloadStatus.Downloading)
                .TransitionTo(DownloadStatus.Failed, "Network error");

            await repository.SaveAsync(job);
            var saved = await repository.GetAsync(job.Id);

            Assert.NotNull(saved);
            Assert.Equal(job.Id, saved.Id);
            Assert.Equal(job.Source, saved.Source);
            Assert.Equal(job.DestinationPath, saved.DestinationPath);
            Assert.Equal(DownloadStatus.Failed, saved.Status);
            Assert.Equal("Network error", saved.ErrorMessage);
            Assert.Equal(job.CreatedAt, saved.CreatedAt);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Save_and_get_preserves_video_id_and_quality_preset()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            await repository.InitializeAsync();
            var preparedSourcePath = Path.Combine(root.FullName, "prepared-source.mp4");
            var job = DownloadJob.Create("https://cdn.example/video.mp4", root.FullName, VideoQualityPreset.P720, "abcdefghijk", preparedSourcePath);

            await repository.SaveAsync(job);
            var saved = await repository.GetAsync(job.Id);

            Assert.NotNull(saved);
            Assert.Equal("abcdefghijk", saved.YouTubeVideoId);
            Assert.Equal(VideoQualityPreset.P720, saved.Preset);
            Assert.Equal(preparedSourcePath, saved.PreparedSourcePath);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Initialize_adds_new_columns_without_discarding_old_history()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var databasePath = Path.Combine(root.FullName, "history.db");
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE downloads (
                        id TEXT NOT NULL PRIMARY KEY, source TEXT NOT NULL, destination_path TEXT NOT NULL,
                        status TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL, error_message TEXT NULL
                    );
                    INSERT INTO downloads VALUES ($id, $source, $destination, 'Completed', $created, $updated, NULL);
                    """;
                command.Parameters.AddWithValue("$id", id.ToString("D"));
                command.Parameters.AddWithValue("$source", "legacy-source.mp4");
                command.Parameters.AddWithValue("$destination", "legacy-output.mp4");
                command.Parameters.AddWithValue("$created", now.ToString("O"));
                command.Parameters.AddWithValue("$updated", now.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }

            var repository = new SqliteDownloadHistoryRepository(databasePath);
            await repository.InitializeAsync();
            var saved = await repository.GetAsync(id);

            Assert.NotNull(saved);
            Assert.Equal("legacy-source.mp4", saved.Source);
            Assert.Equal(DownloadStatus.Completed, saved.Status);
            Assert.Equal(VideoQualityPreset.Original, saved.Preset);
            Assert.Null(saved.YouTubeVideoId);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Saved_history_survives_repository_reopen()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var databasePath = Path.Combine(root.FullName, "history.db");
            var job = DownloadJob.Create("local:source.mp4", Path.Combine(root.FullName, "copy.mp4"));
            var first = new SqliteDownloadHistoryRepository(databasePath);
            await first.InitializeAsync();
            await first.SaveAsync(job);

            var reopened = new SqliteDownloadHistoryRepository(databasePath);
            await reopened.InitializeAsync();
            var jobs = await reopened.GetAllAsync();

            Assert.Single(jobs);
            Assert.Equal(job.Id, jobs[0].Id);
            Assert.Equal(DownloadStatus.Queued, jobs[0].Status);
        }
        finally { root.Delete(true); }
    }
}
