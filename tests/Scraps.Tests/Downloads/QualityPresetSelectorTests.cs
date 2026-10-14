using Scraps.Application.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class QualityPresetSelectorTests
{
    [Fact]
    public void GetAvailable_offers_original_and_presets_up_to_source_height()
    {
        var available = QualityPresetSelector.GetAvailable(new VideoInfo(1920, 1080, TimeSpan.FromSeconds(5)));

        Assert.Equal([VideoQualityPreset.Original, VideoQualityPreset.P1080, VideoQualityPreset.P720, VideoQualityPreset.P480, VideoQualityPreset.P360], available);
    }

    [Fact]
    public void GetAvailable_does_not_offer_upscale_presets()
    {
        var available = QualityPresetSelector.GetAvailable(new VideoInfo(854, 480, TimeSpan.FromSeconds(5)));

        Assert.Equal([VideoQualityPreset.Original, VideoQualityPreset.P480, VideoQualityPreset.P360], available);
    }

    [Fact]
    public void GetAvailable_keeps_only_original_for_source_shorter_than_smallest_preset()
    {
        var available = QualityPresetSelector.GetAvailable(new VideoInfo(320, 240, TimeSpan.FromSeconds(2)));

        Assert.Equal([VideoQualityPreset.Original], available);
    }
}
