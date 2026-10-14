using Scraps.Application.Downloads;

namespace Scraps.Tests.Downloads;

public sealed class YouTubeLinkParserTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abcdefghijk", "abcdefghijk")]
    [InlineData("https://youtu.be/abcdefghijk?t=15", "abcdefghijk")]
    [InlineData("https://youtube.com/shorts/abcdefghijk", "abcdefghijk")]
    [InlineData("https://m.youtube.com/live/abcdefghijk?feature=share", "abcdefghijk")]
    public void TryParse_extracts_video_id_from_supported_urls(string input, string expected)
    {
        var parsed = YouTubeLinkParser.TryParse(input, out var videoId);

        Assert.True(parsed);
        Assert.Equal(expected, videoId);
    }

    [Theory]
    [InlineData("https://youtube.com.evil.example/watch?v=abcdefghijk")]
    [InlineData("https://notyoutube.com/watch?v=abcdefghijk")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://youtu.be/")]
    [InlineData("file:///watch?v=abcdefghijk")]
    [InlineData("not a URL")]
    public void TryParse_rejects_unsupported_or_untrusted_urls(string input)
    {
        var parsed = YouTubeLinkParser.TryParse(input, out var videoId);

        Assert.False(parsed);
        Assert.Equal(string.Empty, videoId);
    }
}
