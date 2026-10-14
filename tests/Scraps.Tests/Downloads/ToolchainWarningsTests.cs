using Scraps.Application.Downloads;

namespace Scraps.Tests.Downloads;

public class ToolchainWarningsTests
{
    [Fact]
    public void No_warnings_when_ffmpeg_and_ffprobe_are_available()
    {
        var warnings = ToolchainWarnings.From(new MediaToolchainStatus(FfmpegAvailable: true, FfprobeAvailable: true));

        Assert.Empty(warnings);
    }

    [Fact]
    public void Missing_ffmpeg_warning_names_the_fallback_and_the_official_download()
    {
        var warnings = ToolchainWarnings.From(new MediaToolchainStatus(FfmpegAvailable: false, FfprobeAvailable: true));

        var warning = Assert.Single(warnings);
        Assert.StartsWith("ffmpeg não encontrado.", warning, StringComparison.Ordinal);
        Assert.Contains(ToolchainWarnings.FfmpegDownloadUrl, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_ffprobe_warning_points_to_the_ffmpeg_package()
    {
        var warnings = ToolchainWarnings.From(new MediaToolchainStatus(FfmpegAvailable: true, FfprobeAvailable: false));

        var warning = Assert.Single(warnings);
        Assert.StartsWith("ffprobe não encontrado.", warning, StringComparison.Ordinal);
        Assert.Contains(ToolchainWarnings.FfmpegDownloadUrl, warning, StringComparison.Ordinal);
    }
}
