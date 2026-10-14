using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Scraps.Application.Downloads;

public sealed class DownloadCoordinator : IAsyncDisposable
{
    private readonly IDownloadHistoryRepository _history;
    private readonly IDownloadExecutor _executor;
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _active = new();
    private readonly object _activeLock = new();
    // Registrado antes de entrar na fila, para que a conclusão nunca aconteça sem um waiter que a veja.
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<DownloadJob>> _completions = new();
    private Task? _worker;
    private bool _stopped;

    public DownloadCoordinator(IDownloadHistoryRepository history, IDownloadExecutor executor)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public event Action<Guid, DownloadProgress>? ProgressChanged;

    // Disparado uma vez por job quando ele chega a Completed, Failed ou Cancelled.
    public event Action<DownloadJob>? JobFinished;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_worker is not null) throw new InvalidOperationException("The coordinator has already started.");
        await _history.InitializeAsync(cancellationToken);
        await RecoverInterruptedAsync(cancellationToken);
        _worker = Task.Run(() => ProcessQueueAsync(_shutdown.Token), CancellationToken.None);
    }

    public async Task<DownloadJob> EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_worker is null || _stopped) throw new InvalidOperationException("Start the coordinator before enqueueing work.");
        cancellationToken.ThrowIfCancellationRequested();

        var job = DownloadJob.Create(request.Source, request.DestinationDirectory, request.Preset, request.YouTubeVideoId, request.PreparedSourcePath);
        await _history.SaveAsync(job, cancellationToken);
        _completions[job.Id] = new TaskCompletionSource<DownloadJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync(job.Id, CancellationToken.None);
        return job;
    }

    // Jobs ainda acompanhados aguardam sua conclusão; jobs já encerrados são lidos do histórico.
    public async Task<DownloadJob> WaitForFinishedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_completions.TryGetValue(jobId, out var completion))
            return await completion.Task.WaitAsync(cancellationToken);

        var job = await _history.GetAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException("O job não foi encontrado no histórico.");
        if (job.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
            return job;
        throw new InvalidOperationException("O job não está sendo acompanhado pelo coordenador.");
    }

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _history.GetAsync(jobId, cancellationToken);
        if (job is null) return false;

        if (job.Status == DownloadStatus.Queued)
        {
            var cancelled = job.TransitionTo(DownloadStatus.Cancelled);
            await _history.SaveAsync(cancelled, cancellationToken);
            Finish(cancelled);
            return true;
        }

        if (job.Status == DownloadStatus.Downloading)
        {
            lock (_activeLock)
            {
                if (_active.TryGetValue(jobId, out var source))
                {
                    source.Cancel();
                    return true;
                }
            }
        }

        return false;
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _history.GetAllAsync(cancellationToken);
        foreach (var job in jobs.Where(job => job.Status is DownloadStatus.Queued or DownloadStatus.Downloading))
        {
            await _history.SaveAsync(job.TransitionTo(DownloadStatus.Failed, "Interrupted by application shutdown."), cancellationToken);
        }
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        if (_worker is not null)
        {
            try { await _worker; }
            catch (OperationCanceledException) { }
        }
    }

    private async Task ProcessQueueAsync(CancellationToken shutdownToken)
    {
        try
        {
            await foreach (var jobId in _queue.Reader.ReadAllAsync(shutdownToken))
            {
                var job = await _history.GetAsync(jobId, shutdownToken);
                if (job?.Status != DownloadStatus.Queued) continue;

                var downloading = job.TransitionTo(DownloadStatus.Downloading);
                await _history.SaveAsync(downloading, shutdownToken);
                using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                lock (_activeLock) _active[jobId] = jobCancellation;

                try
                {
                    var progress = new InlineProgress<DownloadProgress>(value => PublishProgress(jobId, value));
                    var destinationPath = await _executor.ExecuteAsync(downloading, progress, jobCancellation.Token);
                    var completed = downloading.TransitionTo(DownloadStatus.Completed, destinationPath: destinationPath);
                    await _history.SaveAsync(completed, CancellationToken.None);
                    Finish(completed);
                }
                catch (OperationCanceledException) when (jobCancellation.IsCancellationRequested)
                {
                    var cancelled = downloading.TransitionTo(DownloadStatus.Cancelled);
                    await _history.SaveAsync(cancelled, CancellationToken.None);
                    Finish(cancelled);
                }
                catch (Exception exception)
                {
                    var failed = downloading.TransitionTo(DownloadStatus.Failed, exception.Message);
                    await _history.SaveAsync(failed, CancellationToken.None);
                    Finish(failed);
                }
                finally
                {
                    lock (_activeLock) _active.Remove(jobId);
                }
            }
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            // Shutdown is an expected signal; unfinished queued jobs are marked failed on next start.
        }
    }

    // Observadores (UI, testes) rodam antes de liberar os waiters, para que quem espera já veja o evento.
    private void Finish(DownloadJob finished)
    {
        PublishJobFinished(finished);
        if (_completions.TryRemove(finished.Id, out var completion))
            completion.TrySetResult(finished);
    }

    private void PublishJobFinished(DownloadJob finished)
    {
        var handlers = JobFinished;
        if (handlers is null) return;
        foreach (Action<DownloadJob> handler in handlers.GetInvocationList())
        {
            try { handler(finished); }
            catch { /* Observadores não podem derrubar o worker nem deixar o job sem conclusão. */ }
        }
    }

    private void PublishProgress(Guid jobId, DownloadProgress progress)
    {
        var handlers = ProgressChanged;
        if (handlers is null) return;
        foreach (Action<Guid, DownloadProgress> handler in handlers.GetInvocationList())
        {
            try { handler(jobId, progress); }
            catch { /* UI observers must not fail an active media transfer. */ }
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _shutdown.Dispose();
    }
}
