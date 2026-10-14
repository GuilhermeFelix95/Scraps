using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class SqliteDownloadHistoryPageTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPage_returns_newest_first_and_a_cursor_until_the_last_page()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await CreateRepositoryAsync(root);
            var jobs = Enumerable.Range(0, 5)
                .Select(index => CreateJob(root, index, BaseTime.AddMinutes(index)))
                .ToList();
            foreach (var job in jobs) await repository.SaveAsync(job);

            var first = await repository.GetPageAsync(cursor: null, pageSize: 2);
            var second = await repository.GetPageAsync(first.NextCursor, pageSize: 2);
            var third = await repository.GetPageAsync(second.NextCursor, pageSize: 2);

            var expected = jobs.OrderByDescending(job => job.CreatedAt).Select(job => job.Id).ToList();
            var actual = first.Items.Concat(second.Items).Concat(third.Items).Select(job => job.Id).ToList();
            Assert.Equal(expected, actual);
            Assert.NotNull(first.NextCursor);
            Assert.NotNull(second.NextCursor);
            Assert.Null(third.NextCursor);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task GetPage_does_not_repeat_or_skip_jobs_that_share_a_creation_time()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await CreateRepositoryAsync(root);
            var jobs = Enumerable.Range(0, 3).Select(index => CreateJob(root, index, BaseTime)).ToList();
            foreach (var job in jobs) await repository.SaveAsync(job);

            var seen = new List<Guid>();
            string? cursor = null;
            do
            {
                var page = await repository.GetPageAsync(cursor, pageSize: 1);
                seen.AddRange(page.Items.Select(job => job.Id));
                cursor = page.NextCursor;
            } while (cursor is not null);

            Assert.Equal(3, seen.Count);
            Assert.Equal(jobs.Select(job => job.Id).OrderBy(id => id), seen.OrderBy(id => id));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task GetPage_rejects_a_cursor_it_did_not_issue()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await CreateRepositoryAsync(root);

            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetPageAsync("não-é-cursor", pageSize: 2));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task GetPage_rejects_page_sizes_outside_the_allowed_range()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await CreateRepositoryAsync(root);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetPageAsync(null, pageSize: 0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetPageAsync(null, pageSize: 201));
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static async Task<SqliteDownloadHistoryRepository> CreateRepositoryAsync(DirectoryInfo root)
    {
        var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
        await repository.InitializeAsync();
        return repository;
    }

    private static DownloadJob CreateJob(DirectoryInfo root, int index, DateTimeOffset createdAt) => DownloadJob.Rehydrate(
        Guid.NewGuid(),
        $"https://cdn.example/{index}.mp4",
        Path.Combine(root.FullName, $"{index}.mp4"),
        DownloadStatus.Completed,
        createdAt,
        createdAt,
        errorMessage: null);
}
