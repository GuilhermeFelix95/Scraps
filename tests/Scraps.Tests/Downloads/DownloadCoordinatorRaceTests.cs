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
            var history = new GatedHistoryRepository(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")), DownloadStatus.Downloading);
            await using var coordinator = new DownloadCoordinator(history, new SlowExecutor(TimeSpan.FromSeconds(2), Path.Combine(root.FullName, "race.mp4")));
            await coordinator.StartAsync();
            // Armado antes do Enqueue: o worker pode reivindicar o job assim que ele entra na fila.
            history.HoldNextSave();
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/race.mp4", root.FullName));

            await history.SaveHeld.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cancel = coordinator.CancelAsync(job.Id);
            // O gate mantém o cancelamento esperando enquanto a gravação de Downloading está pendente: ele não lê o job nesse intervalo.
            // Pausa fixa de 300 ms: a asserção fica verdadeira mesmo que o cancelamento demore; não prova que a leitura ocorreria depois.
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.False(history.ReadWhileHeld.Task.IsCompleted, "O cancelamento leu o job durante a reivindicação, sem esperar o gate.");
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

    [Fact]
    public async Task Cancel_after_the_download_finished_returns_false()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var history = new GatedHistoryRepository(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")), DownloadStatus.Completed);
            await using var coordinator = new DownloadCoordinator(history, new SlowExecutor(TimeSpan.FromMilliseconds(10), Path.Combine(root.FullName, "done.mp4")));
            await coordinator.StartAsync();
            history.HoldNextSave();
            var job = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/done.mp4", root.FullName));

            // Com a gravação de Completed pendente, o download já acabou: o pedido de cancelamento não pode ser aceito.
            await history.SaveHeld.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var accepted = await coordinator.CancelAsync(job.Id);
            history.ReleaseHeldSave();

            var finished = await coordinator.WaitForFinishedAsync(job.Id);
            Assert.False(accepted);
            Assert.Equal(DownloadStatus.Completed, finished.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task A_database_error_while_claiming_one_job_does_not_stop_the_next_job()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var history = new FailFirstReadHistoryRepository(new SqliteDownloadHistoryRepository(Path.Combine(root.FullName, "history.db")));
            await using var coordinator = new DownloadCoordinator(history, new SlowExecutor(TimeSpan.FromMilliseconds(10), Path.Combine(root.FullName, "next.mp4")));
            await coordinator.StartAsync();

            var first = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/first.mp4", root.FullName));
            var second = await coordinator.EnqueueAsync(new DownloadRequest("https://media.example/next.mp4", root.FullName));

            // O job que falhou na reivindicação termina como Failed, e quem espera por ele não fica travado.
            var failed = await coordinator.WaitForFinishedAsync(first.Id).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(DownloadStatus.Failed, failed.Status);
            Assert.Equal("database is locked", failed.ErrorMessage);

            var finished = await coordinator.WaitForFinishedAsync(second.Id).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(DownloadStatus.Completed, finished.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    // Repositório cuja primeira leitura falha, como um "database is locked" durante a reivindicação.
    private sealed class FailFirstReadHistoryRepository(IDownloadHistoryRepository inner) : IDownloadHistoryRepository
    {
        private bool _failed;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);

        public Task SaveAsync(DownloadJob job, CancellationToken cancellationToken = default) => inner.SaveAsync(job, cancellationToken);

        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException("database is locked");
            }
            return inner.GetAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);

        public Task<DownloadHistoryPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default) =>
            inner.GetPageAsync(cursor, pageSize, cancellationToken);
    }

    // Repositório que segura a primeira gravação do status dado, para forçar a janela da corrida.
    private sealed class GatedHistoryRepository(IDownloadHistoryRepository inner, DownloadStatus holdStatus) : IDownloadHistoryRepository
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _holdNext;

        public TaskCompletionSource SaveHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadWhileHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void HoldNextSave() => _holdNext = true;

        public void ReleaseHeldSave() => _release.TrySetResult();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);

        public async Task SaveAsync(DownloadJob job, CancellationToken cancellationToken = default)
        {
            if (_holdNext && job.Status == holdStatus)
            {
                _holdNext = false;
                SaveHeld.TrySetResult();
                await _release.Task;
            }
            await inner.SaveAsync(job, cancellationToken);
        }

        public async Task<DownloadJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (SaveHeld.Task.IsCompletedSuccessfully && !_release.Task.IsCompleted)
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
