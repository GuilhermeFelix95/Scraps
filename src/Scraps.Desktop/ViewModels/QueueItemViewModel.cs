using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Uma linha da fila. O cancelamento é delegado ao coordenador; o item só reflete o estado que ele informa.
public sealed partial class QueueItemViewModel : ObservableObject
{
    private readonly Func<Guid, Task> _cancel;

    public QueueItemViewModel(Guid id, string label, DownloadStatus status, Func<Guid, Task> cancel)
    {
        Id = id;
        Label = label;
        Status = status;
        _cancel = cancel;
    }

    public Guid Id { get; }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private DownloadStatus _status;

    // Fração de 0 a 1; permanece em 0 quando o executor não informa progresso.
    [ObservableProperty]
    private double _progress;

    public bool IsActive => Status is DownloadStatus.Queued or DownloadStatus.Downloading;

    public string StatusText => Status switch
    {
        DownloadStatus.Queued => "Na fila",
        DownloadStatus.Downloading => "Baixando",
        DownloadStatus.Completed => "Concluído",
        DownloadStatus.Failed => "Falhou",
        DownloadStatus.Cancelled => "Cancelado",
        _ => Status.ToString()
    };

    [RelayCommand(CanExecute = nameof(IsActive))]
    private Task CancelAsync() => _cancel(Id);
}
