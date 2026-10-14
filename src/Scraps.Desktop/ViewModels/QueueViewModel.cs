using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Fila exibida na UI. Progresso chega na thread do worker; cada atualização passa por "post" para a thread de interface.
public sealed class QueueViewModel : IDisposable
{
    private readonly DownloadCoordinator _coordinator;
    private readonly Action<Action> _post;
    private readonly ConcurrentDictionary<Guid, QueueItemViewModel> _items = new();

    public QueueViewModel(DownloadCoordinator coordinator, Action<Action> post)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _coordinator.ProgressChanged += OnProgressChanged;
        _coordinator.JobStarted += OnJobStarted;
    }

    public ObservableCollection<QueueItemViewModel> Jobs { get; } = [];

    public void Track(DownloadJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var item = new QueueItemViewModel(job.Id, job.Source, job.Status, CancelAsync);
        _items[job.Id] = item;
        Jobs.Add(item);
        // Também cobre o job que já terminou antes de entrar na fila: WaitForFinishedAsync devolve o estado final.
        _ = FollowAsync(job.Id);
    }

    public void Dispose()
    {
        _coordinator.ProgressChanged -= OnProgressChanged;
        _coordinator.JobStarted -= OnJobStarted;
    }

    // O coordenador responde false quando o job já não está ativo; o estado final chega por WaitForFinishedAsync.
    private async Task CancelAsync(Guid jobId) => await _coordinator.CancelAsync(jobId);

    private async Task FollowAsync(Guid jobId)
    {
        // O job pode já ter começado ou terminado antes do Track: o estado atual cobre o que os eventos perderam.
        var current = await _coordinator.GetJobAsync(jobId);
        if (current is not null) _post(() => Apply(current));
        var finished = await _coordinator.WaitForFinishedAsync(jobId);
        _post(() => Apply(finished));
    }

    private void OnJobStarted(DownloadJob job) => _post(() => Apply(job));

    private void Apply(DownloadJob job)
    {
        if (!_items.TryGetValue(job.Id, out var item)) return;
        // Leituras atrasadas não podem devolver um job já iniciado para Queued.
        if (job.Status == DownloadStatus.Queued && item.Status != DownloadStatus.Queued) return;
        item.Status = job.Status;
        if (job.Status == DownloadStatus.Completed) item.Progress = 1;
    }

    private void OnProgressChanged(Guid jobId, DownloadProgress progress)
    {
        if (progress.Fraction is not double fraction) return;
        _post(() =>
        {
            if (_items.TryGetValue(jobId, out var item)) item.Progress = fraction;
        });
    }
}
