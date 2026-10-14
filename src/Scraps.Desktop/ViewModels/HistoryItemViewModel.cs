using CommunityToolkit.Mvvm.Input;
using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Uma linha do histórico. Abrir a pasta só fica disponível enquanto ela existir no disco.
public sealed class HistoryItemViewModel
{
    private readonly Action<string> _openFolder;

    public HistoryItemViewModel(DownloadJob job, Action<string> openFolder)
    {
        _openFolder = openFolder;
        Label = job.Source;
        StatusText = DownloadStatusText.For(job.Status);
        ErrorMessage = job.ErrorMessage;
        CreatedAt = job.CreatedAt;
        FolderPath = Path.GetDirectoryName(job.DestinationPath) ?? string.Empty;
        OpenFolderCommand = new RelayCommand(OpenFolder, () => CanOpenFolder);
    }

    public string Label { get; }

    public string StatusText { get; }

    public string? ErrorMessage { get; }

    public DateTimeOffset CreatedAt { get; }

    public string FolderPath { get; }

    public IRelayCommand OpenFolderCommand { get; }

    private bool CanOpenFolder => FolderPath.Length > 0 && Directory.Exists(FolderPath);

    private void OpenFolder() => _openFolder(FolderPath);
}
