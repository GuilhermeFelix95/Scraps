using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Texto de apresentação dos estados: a regra fica no enum, o idioma da tela fica aqui.
internal static class DownloadStatusText
{
    public static string For(DownloadStatus status) => status switch
    {
        DownloadStatus.Queued => "Na fila",
        DownloadStatus.Downloading => "Baixando",
        DownloadStatus.Completed => "Concluído",
        DownloadStatus.Failed => "Falhou",
        DownloadStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };
}
