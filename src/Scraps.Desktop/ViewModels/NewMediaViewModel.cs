using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Fluxo da tela de nova mídia: consulta a origem, escolhe a qualidade e enfileira. Regra de negócio fica na Application.
public sealed partial class NewMediaViewModel(
    MediaImportService import,
    DownloadCoordinator coordinator,
    string destinationDirectory,
    Action<DownloadJob>? onEnqueued = null) : ObservableObject
{
    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private bool _isAuthorized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _videoDescription;

    [ObservableProperty]
    private PresetOption? _selectedOption;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnqueueCommand))]
    [NotifyPropertyChangedFor(nameof(HasPreparedMedia))]
    private PreparedMedia? _preparedMedia;

    public bool HasError => ErrorMessage is not null;

    public bool HasPreparedMedia => PreparedMedia is not null;

    public ObservableCollection<PresetOption> Presets { get; } = [];

    [RelayCommand]
    private async Task ConsultAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!IsAuthorized)
        {
            ErrorMessage = "Confirme que você tem autorização para baixar e processar esta mídia.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Source))
        {
            ErrorMessage = "Informe um link do YouTube, um caminho de arquivo ou uma URL direta.";
            return;
        }

        MediaSourceRequest request;
        try
        {
            request = MediaSourceParser.Parse(Source);
        }
        catch (MediaSourceException exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        try
        {
            var media = await import.PrepareAsync(request, cancellationToken);
            ReleasePrepared();
            PreparedMedia = media;
            Title = media.Title ?? media.Source;
            VideoDescription = $"{media.VideoInfo.Width}x{media.VideoInfo.Height} — {media.VideoInfo.Duration:c}";
            Presets.Clear();
            foreach (var preset in media.Presets)
                Presets.Add(new PresetOption(preset, QualityPresetSelector.GetDisplayName(preset)));
            SelectedOption = Presets.FirstOrDefault();
        }
        catch (MediaSourceException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEnqueue))]
    private async Task EnqueueAsync(CancellationToken cancellationToken)
    {
        if (PreparedMedia is null || SelectedOption is null) return;
        ErrorMessage = null;

        var media = PreparedMedia;
        var request = media.CreateRequest(SelectedOption.Preset, destinationDirectory);
        // A partir daqui a fila é dona da pasta de inspeção: a tela não pode descartá-la.
        PreparedMedia = null;
        var job = await coordinator.EnqueueAsync(request, cancellationToken);
        StatusMessage = "Trabalho enfileirado.";
        onEnqueued?.Invoke(job);
        _ = FollowJobAsync(job.Id, media);
    }

    private bool CanEnqueue => PreparedMedia is not null;

    private async Task FollowJobAsync(Guid jobId, PreparedMedia media)
    {
        try
        {
            var finished = await coordinator.WaitForFinishedAsync(jobId);
            StatusMessage = finished.Status switch
            {
                DownloadStatus.Completed => $"Concluído: {finished.DestinationPath}",
                DownloadStatus.Failed => $"Falha: {finished.ErrorMessage}",
                _ => "Operação cancelada."
            };
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            media.Dispose();
        }
    }

    private void ReleasePrepared()
    {
        var previous = PreparedMedia;
        PreparedMedia = null;
        previous?.Dispose();
    }
}
