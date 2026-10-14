using System.Globalization;
using System.Text.Json;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class FfprobeVideoProbe(IExternalProcessRunner processRunner) : IVideoProbe
{
    public async Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var result = await processRunner.RunAsync("ffprobe",
            ["-v", "error", "-show_streams", "-show_format", "-of", "json", path],
            cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed: {result.StandardError.Trim()}");

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("ffprobe returned no stream list.");

        var video = streams.EnumerateArray().FirstOrDefault(stream =>
            stream.TryGetProperty("codec_type", out var codecType)
            && codecType.GetString()?.Equals("video", StringComparison.OrdinalIgnoreCase) == true);
        if (video.ValueKind != JsonValueKind.Object
            || !video.TryGetProperty("width", out var widthElement)
            || !video.TryGetProperty("height", out var heightElement))
            throw new InvalidDataException("The source does not contain a readable video stream.");

        var width = widthElement.GetInt32();
        var height = heightElement.GetInt32();
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("The video stream has invalid dimensions.");

        if (!root.TryGetProperty("format", out var format)
            || !format.TryGetProperty("duration", out var durationElement)
            || !double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds)
            || !double.IsFinite(durationSeconds) || durationSeconds < 0)
            throw new InvalidDataException("ffprobe returned an invalid video duration.");

        return new VideoInfo(width, height, TimeSpan.FromSeconds(durationSeconds));
    }
}
