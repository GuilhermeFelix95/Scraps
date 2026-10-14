using System.Net;
using Scraps.Application.Downloads;
using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Desktop;

public sealed class NewMediaViewModelTests
{
    [Fact]
    public async Task Consult_without_authorization_shows_error_and_does_not_probe()
    {
        var probe = new CountingProbe();
        var viewModel = CreateViewModel(probe: probe);
        viewModel.Source = "https://youtu.be/abcdefghijk";
        viewModel.IsAuthorized = false;

        await viewModel.ConsultCommand.ExecuteAsync(null);

        Assert.Equal(0, probe.Calls);
        Assert.Contains("autorização", viewModel.ErrorMessage);
        Assert.Empty(viewModel.Presets);
    }

    [Fact]
    public async Task Consult_with_invalid_youtube_link_shows_parser_message()
    {
        var viewModel = CreateViewModel();
        viewModel.Source = "https://youtu.be/abcdefghijk/extra";
        viewModel.IsAuthorized = true;

        await viewModel.ConsultCommand.ExecuteAsync(null);

        Assert.Contains("Link do YouTube inválido", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Consult_loads_title_and_presets_then_selects_first_option()
    {
        var viewModel = CreateViewModel();
        viewModel.Source = "https://youtu.be/abcdefghijk";
        viewModel.IsAuthorized = true;

        await viewModel.ConsultCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Sample video", viewModel.Title);
        Assert.Contains(VideoQualityPreset.P720, viewModel.Presets.Select(option => option.Preset));
        Assert.Equal(viewModel.Presets[0], viewModel.SelectedOption);
    }

    [Fact]
    public async Task Enqueue_creates_job_and_reports_completion()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new ImmediateExecutor(Path.Combine(root.FullName, "result.mp4"));
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var viewModel = CreateViewModel(coordinator: coordinator, destination: root.FullName);
            viewModel.Source = "https://youtu.be/abcdefghijk";
            viewModel.IsAuthorized = true;
            await viewModel.ConsultCommand.ExecuteAsync(null);

            await viewModel.EnqueueCommand.ExecuteAsync(null);
            await WaitUntil(() => viewModel.StatusMessage.StartsWith("Concluído", StringComparison.Ordinal));

            Assert.NotNull(executor.LastJob);
            Assert.Equal("abcdefghijk", executor.LastJob.YouTubeVideoId);
            Assert.Null(viewModel.ErrorMessage);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Enqueue_without_consult_does_nothing()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var repository = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            var executor = new ImmediateExecutor(Path.Combine(root.FullName, "result.mp4"));
            await using var coordinator = new DownloadCoordinator(repository, executor);
            await coordinator.StartAsync();
            var viewModel = CreateViewModel(coordinator: coordinator, destination: root.FullName);

            Assert.False(viewModel.EnqueueCommand.CanExecute(null));
            Assert.Null(executor.LastJob);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static NewMediaViewModel CreateViewModel(
        CountingProbe? probe = null,
        DownloadCoordinator? coordinator = null,
        string? destination = null)
    {
        var videoInfo = new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2));
        var youTube = new StubYouTubeVideoSource(new YouTubeVideoMetadata("abcdefghijk", "Sample video", videoInfo));
        var import = new MediaImportService(youTube, probe ?? new CountingProbe(), new NoDirectDownloads(), toolchain: null);
        var downloadCoordinator = coordinator ?? new DownloadCoordinator(
            new SqliteDownloadHistoryRepository(Path.Combine(Path.GetTempPath(), $"scraps-test-{Guid.NewGuid():N}.db")),
            new ImmediateExecutor(string.Empty));
        return new NewMediaViewModel(import, downloadCoordinator, destination ?? Path.GetTempPath());
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(50);
        Assert.True(condition(), "A condição esperada não ocorreu a tempo.");
    }

    private sealed class CountingProbe : IVideoProbe
    {
        public int Calls { get; private set; }

        public Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(2)));
        }
    }

    private sealed class StubYouTubeVideoSource(YouTubeVideoMetadata metadata) : IYouTubeVideoSource
    {
        public Task<YouTubeVideoMetadata> GetMetadataAsync(Uri source, CancellationToken cancellationToken) => Task.FromResult(metadata);

        public Task<string> DownloadAsync(Uri source, string expectedVideoId, string stagingDirectory,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Não deve baixar na preparação.");
    }

    private sealed class NoDirectDownloads : IDirectMediaDownloader
    {
        public Task<string> DownloadAsync(Uri source, string destinationDirectory, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Não deve baixar neste teste.");
    }

    private sealed class ImmediateExecutor(string result) : IDownloadExecutor
    {
        public DownloadJob? LastJob { get; private set; }

        public Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            LastJob = job;
            return Task.FromResult(result);
        }
    }
}
