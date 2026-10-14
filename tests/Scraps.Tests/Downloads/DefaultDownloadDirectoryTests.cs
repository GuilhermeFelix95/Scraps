using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class DefaultDownloadDirectoryTests
{
    [Fact]
    public void GetPath_uses_downloads_scraps_under_user_profile()
    {
        var profile = Path.Combine(Path.GetTempPath(), "scraps-profile-test");

        var path = DefaultDownloadDirectory.GetPath(profile);

        Assert.Equal(Path.Combine(profile, "Downloads", "Scraps"), path);
    }

    [Fact]
    public void GetPath_rejects_blank_profile_override()
    {
        Assert.Throws<ArgumentException>(() => DefaultDownloadDirectory.GetPath(" "));
    }
}
