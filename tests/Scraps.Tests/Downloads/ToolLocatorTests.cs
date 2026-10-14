using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class ToolLocatorTests
{
    [Fact]
    public void Resolve_prefers_explicit_override_over_bundled_tool()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            CreateBundledTool(root.FullName, "yt-dlp", "yt-dlp");

            var path = ToolLocator.Resolve(@"C:\custom\yt-dlp.exe", root.FullName, "yt-dlp", "yt-dlp");

            Assert.Equal(@"C:\custom\yt-dlp.exe", path);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Resolve_uses_bundled_tool_in_third_party_folder_when_present()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var bundled = CreateBundledTool(root.FullName, "ffmpeg", "ffprobe");

            var path = ToolLocator.Resolve(null, root.FullName, "ffmpeg", "ffprobe");

            Assert.Equal(bundled, path);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Resolve_falls_back_to_bare_name_so_path_lookup_applies_when_nothing_is_bundled()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var path = ToolLocator.Resolve(null, root.FullName, "yt-dlp", "yt-dlp");

            Assert.Equal(ToolLocator.ExecutableName("yt-dlp"), path);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Resolve_ignores_blank_override()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var path = ToolLocator.Resolve("   ", root.FullName, "ffmpeg", "ffmpeg");

            Assert.Equal(ToolLocator.ExecutableName("ffmpeg"), path);
        }
        finally { root.Delete(true); }
    }

    private static string CreateBundledTool(string baseDirectory, string folder, string toolName)
    {
        var directory = Path.Combine(baseDirectory, "third-party", folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ToolLocator.ExecutableName(toolName));
        File.WriteAllText(path, "fake tool");
        return path;
    }
}
