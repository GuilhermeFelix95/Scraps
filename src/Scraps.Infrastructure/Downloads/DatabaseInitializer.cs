using Microsoft.Data.Sqlite;

namespace Scraps.Infrastructure.Downloads;

public static class DatabaseInitializer
{
    public static string GetDefaultDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Scraps",
        "history.db");

    public static async Task InitializeAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS downloads (
                id TEXT NOT NULL PRIMARY KEY,
                source TEXT NOT NULL,
                destination_path TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                error_message TEXT NULL,
                youtube_video_id TEXT NULL,
                preset TEXT NOT NULL DEFAULT 'Original',
                prepared_source_path TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_downloads_created_at ON downloads(created_at DESC);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "youtube_video_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "preset", "TEXT NOT NULL DEFAULT 'Original'", cancellationToken);
        await EnsureColumnAsync(connection, "prepared_source_path", "TEXT NULL", cancellationToken);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string columnName, string definition, CancellationToken cancellationToken)
    {
        await using var inspect = connection.CreateCommand();
        inspect.CommandText = "PRAGMA table_info(downloads);";
        await using var reader = await inspect.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase)) return;
        }
        await reader.DisposeAsync();

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE downloads ADD COLUMN {columnName} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }
}
