using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class DownloadCoordinatorRaceTests
{
    [Fact]
    public async Task Cancel_during_the_worker_claim_is_not_lost()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var history = new GatedHistoryRepository(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")));
            await using var coordinator = new DownloadCoordinator(history, new SlowExecutor(TimeSpan.FromSeconds(2), Path.Combine(root.FullName, "race.mp4")));
            await coordinator.StartAsync();
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/race.mp4", root.FullName));

            history.HoldNextDownloadingSave();
            await history.DownloadingSaveHeld.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cancel = coordinator.CancelAsync(job.Id);
            // Se o cancelamento lê o job enquanto a gravação de Downloading está pendente, ele enxerga Queued: é a corrida.
            await Task.WhenAny(history.ReadWhileHeld.Task, Task.Delay(TimeSpan.FromMilliseconds(300)));
            history.ReleaseHeldSave();
            await cancel;

            var finished = await coordinator.WaitForFinishedAsync(job.Id);
            var stored = await history.GetAsync(job.Id);
            Assert.Equal(DownloadStatus.Cancelled, finished.Status);
            Assert.Equal(DownloadStatus.Cancelled, stored!.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task JobStarted_is_raised_when_the_worker_claims_the_job()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var history = new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db"));
            await using var coordinator = new DownloadCoordinator(history, new SlowExecutor(TimeSpan.FromMilliseconds(10), Path.Combine(root.FullName, "started.mp4")));
            var started = new TaskCompletionSource<DownloadJob>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.JobStarted += job => started.TrySetResult(job);
            await coordinator.StartAsync();

            var queued = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/started.mp4", root.FullName));
            var running = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(queued.Id, running.Id);
            Assert.Equal(DownloadStatus.Downloading, running.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    // Repositório que segura a primeira gravação de Downloading do job, para forçar a janela da corrida.
    private sealed class GatedHistoryRepository(IDownloadHistoryRepository inner) : IDownloadHistoryRepository
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _holdNext;

        public TaskCompletionSource DownloadingSaveHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadWhileHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void HoldNextDownloadingSave() => _holdNext = true;

        public void ReleaseHeldSave() => _release.TrySetResult();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);

        public async Task SaveAsync(DownloadJob job, CancellationToken cancellationToken = default)
        {
            if (_holdNext && job.Status == DownloadStatus.Downloading)
            {
                _holdNext = false;
                DownloadingSaveHeld.TrySetResult();
                await _release.Task;
            }
            await inner.SaveAsync(job, cancellationToken);
        }

        public async Task<DownloadJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (DownloadingSaveHeld.Task.IsCompletedSuccessfully && !_release.Task.IsCompleted)
                ReadWhileHeld.TrySetResult();
            return await inner.GetAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);

        public Task<DownloadHistoryPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default) =>
            inner.GetPageAsync(cursor, pageSize, cancellationToken);
    }

    // Executor que dura o tempo dado, a menos que seja cancelado; depois entrega o arquivo.
    private sealed class SlowExecutor(TimeSpan duration, string destinationPath) : IDownloadExecutor
    {
        public async Task<string> ExecuteAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            await Task.Delay(duration, cancellationToken);
            return destinationPath;
        }
    }
}
