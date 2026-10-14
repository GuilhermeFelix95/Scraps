using Scraps.Application.Downloads;
using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Desktop;

public sealed class HistoryViewModelTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Refresh_loads_first_page_and_load_next_page_appends_the_rest()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await SeedAsync(root, count: 3);
            var viewModel = new HistoryViewModel(repository, openFolder: _ => { }, pageSize: 2);

            await viewModel.RefreshCommand.ExecuteAsync(null);
            Assert.Equal(2, viewModel.Items.Count);
            Assert.True(viewModel.HasMore);

            await viewModel.LoadNextPageCommand.ExecuteAsync(null);
            Assert.Equal(3, viewModel.Items.Count);
            Assert.False(viewModel.HasMore);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Refresh_replaces_items_instead_of_duplicating_them()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await SeedAsync(root, count: 2);
            var viewModel = new HistoryViewModel(repository, openFolder: _ => { }, pageSize: 10);

            await viewModel.RefreshCommand.ExecuteAsync(null);
            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.Equal(2, viewModel.Items.Count);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Open_folder_passes_the_file_directory_when_it_exists()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = await SeedAsync(root, count: 1);
            string? opened = null;
            var viewModel = new HistoryViewModel(repository, openFolder: path => opened = path, pageSize: 10);
            await viewModel.RefreshCommand.ExecuteAsync(null);
            var item = viewModel.Items.Single();

            Assert.True(item.OpenFolderCommand.CanExecute(null));
            item.OpenFolderCommand.Execute(null);

            Assert.Equal(root.FullName, opened);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Open_folder_of_a_failed_job_opens_its_destination_directory()
    {
        // Antes de concluir, DestinationPath guarda a pasta de destino, não um arquivo.
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            await repository.InitializeAsync();
            await repository.SaveAsync(DownloadJob.Rehydrate(
                Guid.NewGuid(),
                "https://media.example/failed.mp4",
                root.FullName,
                DownloadStatus.Failed,
                BaseTime,
                BaseTime,
                errorMessage: "falhou"));
            string? opened = null;
            var viewModel = new HistoryViewModel(repository, openFolder: path => opened = path, pageSize: 10);
            await viewModel.RefreshCommand.ExecuteAsync(null);
            var item = viewModel.Items.Single();

            Assert.Equal(root.FullName, item.FolderPath);
            item.OpenFolderCommand.Execute(null);

            Assert.Equal(root.FullName, opened);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Open_folder_is_disabled_when_the_directory_no_longer_exists()
    {
        var root = Directory.CreateTempSubdirectory();
        var repository = await SeedAsync(root, count: 1);
        var viewModel = new HistoryViewModel(repository, openFolder: _ => { }, pageSize: 10);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        root.Delete(true);

        var item = viewModel.Items.Single();

        Assert.False(item.OpenFolderCommand.CanExecute(null));
    }

    private static async Task<SqliteDownloadHistoryRepository> SeedAsync(DirectoryInfo root, int count)
    {
        var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
        await repository.InitializeAsync();
        for (var index = 0; index < count; index++)
        {
            var createdAt = BaseTime.AddMinutes(index);
            var job = DownloadJob.Rehydrate(
                Guid.NewGuid(),
                $"https://media.example/{index}.mp4",
                Path.Combine(root.FullName, $"{index}.mp4"),
                DownloadStatus.Completed,
                createdAt,
                createdAt,
                errorMessage: null);
            await repository.SaveAsync(job);
        }
        return repository;
    }
}
