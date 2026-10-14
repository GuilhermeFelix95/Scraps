using System.Globalization;
using Microsoft.Data.Sqlite;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class SqliteDownloadHistoryRepository : IDownloadHistoryRepository
{
    private readonly string _connectionString;

    public SqliteDownloadHistoryRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath, Pooling = false }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await DatabaseInitializer.InitializeAsync(connection, cancellationToken);
    }

    public async Task SaveAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO downloads (id, source, destination_path, status, created_at, updated_at, error_message, youtube_video_id, preset, prepared_source_path)
            VALUES ($id, $source, $destination, $status, $created, $updated, $error, $youtubeVideoId, $preset, $preparedSourcePath)
            ON CONFLICT(id) DO UPDATE SET
                source = excluded.source,
                destination_path = excluded.destination_path,
                status = excluded.status,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at,
                error_message = excluded.error_message,
                youtube_video_id = excluded.youtube_video_id,
                preset = excluded.preset,
                prepared_source_path = excluded.prepared_source_path;
            """;
        command.Parameters.AddWithValue("$id", job.Id.ToString("D"));
        command.Parameters.AddWithValue("$source", job.Source);
        command.Parameters.AddWithValue("$destination", job.DestinationPath);
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        // UTC na gravação: a ordenação por texto só é correta com um único formato de data e offset.
        command.Parameters.AddWithValue("$created", job.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", job.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$error", (object?)job.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$youtubeVideoId", (object?)job.YouTubeVideoId ?? DBNull.Value);
        command.Parameters.AddWithValue("$preset", job.Preset.ToString());
        command.Parameters.AddWithValue("$preparedSourcePath", (object?)job.PreparedSourcePath ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DownloadJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, source, destination_path, status, created_at, updated_at, error_message, youtube_video_id, preset, prepared_source_path FROM downloads WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadJob(reader) : null;
    }

    public async Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, source, destination_path, status, created_at, updated_at, error_message, youtube_video_id, preset, prepared_source_path FROM downloads ORDER BY created_at DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var jobs = new List<DownloadJob>();
        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(ReadJob(reader));
        }

        return jobs;
    }

    public async Task<DownloadHistoryPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > DownloadHistoryPage.MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"O tamanho da página deve estar entre 1 e {DownloadHistoryPage.MaxPageSize}.");

        var after = cursor is null ? ((string CreatedAt, string Id)?)null : HistoryCursor.Decode(cursor);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Busca uma linha a mais para saber se existe próxima página sem fazer uma segunda consulta.
        command.CommandText = """
            SELECT id, source, destination_path, status, created_at, updated_at, error_message, youtube_video_id, preset, prepared_source_path
            FROM downloads
            WHERE $createdAt IS NULL OR created_at < $createdAt OR (created_at = $createdAt AND id < $id)
            ORDER BY created_at DESC, id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$createdAt", (object?)after?.CreatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", (object?)after?.Id ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", pageSize + 1);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var jobs = new List<DownloadJob>(pageSize + 1);
        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(ReadJob(reader));
        }

        if (jobs.Count <= pageSize)
            return new DownloadHistoryPage(jobs, NextCursor: null);

        jobs.RemoveAt(jobs.Count - 1);
        var last = jobs[^1];
        return new DownloadHistoryPage(jobs, HistoryCursor.Encode(last.CreatedAt, last.Id));
    }

    private static DownloadJob ReadJob(SqliteDataReader reader) => DownloadJob.Rehydrate(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        Enum.Parse<DownloadStatus>(reader.GetString(3), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        Enum.Parse<VideoQualityPreset>(reader.GetString(8), ignoreCase: false),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(9) ? null : reader.GetString(9));
}
