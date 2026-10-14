namespace Scraps.Application.Downloads;

// Avisos sobre ferramentas ausentes. Texto único para a CLI e para a interface.
public static class ToolchainWarnings
{
    public const string FfmpegDownloadUrl = "https://ffmpeg.org/download.html";

    public static IReadOnlyList<string> From(MediaToolchainStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var warnings = new List<string>();
        if (!status.FfmpegAvailable)
            warnings.Add($"ffmpeg não encontrado. Vídeos do YouTube serão baixados em formato único, que pode ter qualidade menor que a fonte. Os presets de conversão (1080p, 720p, 480p, 360p) ficam indisponíveis até que o ffmpeg seja instalado e adicionado ao PATH. Download: {FfmpegDownloadUrl}");
        if (!status.FfprobeAvailable)
            warnings.Add($"ffprobe não encontrado. A análise de vídeos falhará até que ele seja instalado e adicionado ao PATH. Ele vem no mesmo pacote do ffmpeg: {FfmpegDownloadUrl}");
        return warnings;
    }
}
