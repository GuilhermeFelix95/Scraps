namespace Scraps.Application.Downloads;

public static class QualityPresetSelector
{
    private static readonly (VideoQualityPreset Preset, int Height)[] Presets =
    [
        (VideoQualityPreset.P1080, 1080),
        (VideoQualityPreset.P720, 720),
        (VideoQualityPreset.P480, 480),
        (VideoQualityPreset.P360, 360)
    ];

    public static IReadOnlyList<VideoQualityPreset> GetAvailable(VideoInfo source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(source), "Video dimensions must be positive.");

        return [VideoQualityPreset.Original, .. Presets.Where(item => item.Height <= source.Height).Select(item => item.Preset)];
    }

    public static string GetDisplayName(VideoQualityPreset preset) => preset switch
    {
        VideoQualityPreset.Original => "Original",
        VideoQualityPreset.P1080 => "1080p",
        VideoQualityPreset.P720 => "720p",
        VideoQualityPreset.P480 => "480p",
        VideoQualityPreset.P360 => "360p",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };
}
