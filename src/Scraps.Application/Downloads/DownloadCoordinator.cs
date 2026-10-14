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
    private Task? _worker;
    private bool _stopped;

    public DownloadCoordinator(IDownloadHistoryRepository history, IDownloadExecutor executor)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public event Action<Guid, DownloadProgress>? ProgressChanged;

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
        await _queue.Writer.WriteAsync(job.Id, CancellationToken.None);
        return job;
    }

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _history.GetAsync(jobId, cancellationToken);
        if (job is null) return false;

        if (job.Status == DownloadStatus.Queued)
        {
            await _history.SaveAsync(job.TransitionTo(DownloadStatus.Cancelled), cancellationToken);
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
                    await _history.SaveAsync(downloading.TransitionTo(DownloadStatus.Completed, destinationPath: destinationPath), CancellationToken.None);
                }
                catch (OperationCanceledException) when (jobCancellation.IsCancellationRequested)
                {
                    await _history.SaveAsync(downloading.TransitionTo(DownloadStatus.Cancelled), CancellationToken.None);
                }
                catch (Exception exception)
                {
                    await _history.SaveAsync(downloading.TransitionTo(DownloadStatus.Failed, exception.Message), CancellationToken.None);
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
