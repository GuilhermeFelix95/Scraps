using System.Collections.Concurrent;
using System.Diagnostics;
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
    // Serializa a reivindicação do job pelo worker e a decisão de cancelá-lo enquanto ainda está na fila.
    private readonly SemaphoreSlim _stateGate = new(1, 1);
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

    // Disparado quando o worker reivindica o job e ele passa a Downloading.
    public event Action<DownloadJob>? JobStarted;

    // Falha ao processar um job (erro de banco ou de observador). Um job cuja gravação falhou pode ficar sem conclusão até a próxima inicialização.
    public event Action<Guid, Exception>? WorkerFaulted;

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
        var cancelledWhileQueued = await CancelIfQueuedAsync(jobId, cancellationToken);
        if (cancelledWhileQueued is not null)
        {
            Finish(cancelledWhileQueued);
            return true;
        }

        return CancelIfActive(jobId);
    }

    // Sob o mesmo gate do worker: o job não pode ser reivindicado enquanto a decisão de cancelar é tomada.
    private async Task<DownloadJob?> CancelIfQueuedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            var job = await _history.GetAsync(jobId, cancellationToken);
            if (job?.Status != DownloadStatus.Queued) return null;

            var cancelled = job.TransitionTo(DownloadStatus.Cancelled);
            await _history.SaveAsync(cancelled, cancellationToken);
            return cancelled;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private bool CancelIfActive(Guid jobId)
    {
        lock (_activeLock)
        {
            if (!_active.TryGetValue(jobId, out var source)) return false;
            source.Cancel();
            return true;
        }
    }

    // Estado atual do job no histórico, para quem começa a acompanhar um job que já pode ter começado.
    public Task<DownloadJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _history.GetAsync(jobId, cancellationToken);

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
        // O worker só termina por shutdown: cancelamento e falhas por job já são tratados dentro dele.
        if (_worker is not null) await _worker;
    }

    private async Task ProcessQueueAsync(CancellationToken shutdownToken)
    {
        try
        {
            await foreach (var jobId in _queue.Reader.ReadAllAsync(shutdownToken))
            {
                try
                {
                    await ProcessJobAsync(jobId, shutdownToken);
                }
                // Erro de banco num job não pode parar a fila: o job termina como Failed e o próximo roda.
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    RaiseWorkerFault(jobId, exception);
                    await TryFinishAsFailedAsync(jobId, exception);
                }
            }
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            // Shutdown is an expected signal; unfinished queued jobs are marked failed on next start.
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken shutdownToken)
    {
        var claim = await ClaimQueuedAsync(jobId, shutdownToken);
        if (claim is null) return;

        var downloading = claim.Value.Job;
        using var jobCancellation = claim.Value.Cancellation;
        PublishJob(JobStarted, downloading);
        try
        {
            var progress = new InlineProgress<DownloadProgress>(value => PublishProgress(jobId, value));
            var destinationPath = await _executor.ExecuteAsync(downloading, progress, jobCancellation.Token);
            // Depois do download não há mais o que cancelar: a retirada vem antes da gravação final, para que
            // um cancelamento que chegue agora seja recusado em vez de aceito e ignorado.
            RetireActive(jobId);
            var completed = downloading.TransitionTo(DownloadStatus.Completed, destinationPath: destinationPath);
            await _history.SaveAsync(completed, CancellationToken.None);
            Finish(completed);
        }
        catch (OperationCanceledException) when (jobCancellation.IsCancellationRequested)
        {
            RetireActive(jobId);
            var cancelled = downloading.TransitionTo(DownloadStatus.Cancelled);
            await _history.SaveAsync(cancelled, CancellationToken.None);
            Finish(cancelled);
        }
        catch (Exception exception)
        {
            RetireActive(jobId);
            var failed = downloading.TransitionTo(DownloadStatus.Failed, exception.Message);
            await _history.SaveAsync(failed, CancellationToken.None);
            Finish(failed);
        }
        finally
        {
            RetireActive(jobId);
        }
    }

    // Melhor esforço: se o banco continuar indisponível, a falha de recuperação também é reportada e o job só
    // será marcado na próxima inicialização (RecoverInterruptedAsync).
    private async Task TryFinishAsFailedAsync(Guid jobId, Exception exception)
    {
        try
        {
            var job = await _history.GetAsync(jobId, CancellationToken.None);
            if (job is null || job.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled) return;

            var failed = job.TransitionTo(DownloadStatus.Failed, exception.Message);
            await _history.SaveAsync(failed, CancellationToken.None);
            Finish(failed);
        }
        catch (Exception recoveryException)
        {
            RaiseWorkerFault(jobId, recoveryException);
        }
    }

    private void RetireActive(Guid jobId)
    {
        lock (_activeLock) _active.Remove(jobId);
    }

    private void RaiseWorkerFault(Guid jobId, Exception exception)
    {
        var handlers = WorkerFaulted;
        if (handlers is null) return;
        foreach (Action<Guid, Exception> handler in handlers.GetInvocationList())
        {
            try { handler(jobId, exception); }
            catch (Exception handlerException) { Trace.TraceError($"Observador de falha do job {jobId} lançou exceção: {handlerException}"); }
        }
    }

    // Reivindica o job só se ele ainda estiver na fila, e registra o cancelamento antes de liberar o gate.
    private async Task<(DownloadJob Job, CancellationTokenSource Cancellation)?> ClaimQueuedAsync(Guid jobId, CancellationToken shutdownToken)
    {
        await _stateGate.WaitAsync(shutdownToken);
        try
        {
            var job = await _history.GetAsync(jobId, shutdownToken);
            if (job?.Status != DownloadStatus.Queued) return null;

            var downloading = job.TransitionTo(DownloadStatus.Downloading);
            await _history.SaveAsync(downloading, shutdownToken);
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            lock (_activeLock) _active[jobId] = cancellation;
            return (downloading, cancellation);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    // Observadores (UI, testes) rodam antes de liberar os waiters, para que quem espera já veja o evento.
    private void Finish(DownloadJob finished)
    {
        PublishJob(JobFinished, finished);
        if (_completions.TryRemove(finished.Id, out var completion))
            completion.TrySetResult(finished);
    }

    // Observador que falha não pode derrubar o worker nem deixar o job sem conclusão, mas a falha é reportada.
    private void PublishJob(Action<DownloadJob>? handlers, DownloadJob job)
    {
        if (handlers is null) return;
        foreach (Action<DownloadJob> handler in handlers.GetInvocationList())
        {
            try { handler(job); }
            catch (Exception exception) { RaiseWorkerFault(job.Id, exception); }
        }
    }

    // Observadores de progresso (UI) não podem interromper a transferência, mas a falha é reportada.
    private void PublishProgress(Guid jobId, DownloadProgress progress)
    {
        var handlers = ProgressChanged;
        if (handlers is null) return;
        foreach (Action<Guid, DownloadProgress> handler in handlers.GetInvocationList())
        {
            try { handler(jobId, progress); }
            catch (Exception exception) { RaiseWorkerFault(jobId, exception); }
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
