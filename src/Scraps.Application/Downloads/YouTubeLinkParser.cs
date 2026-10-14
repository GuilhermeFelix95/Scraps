namespace Scraps.Application.Downloads;

public static class YouTubeLinkParser
{
    private static readonly HashSet<string> SupportedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtu.be"
    };

    public static bool TryParse(string input, out string videoId)
    {
        videoId = string.Empty;
        if (string.IsNullOrWhiteSpace(input)
            || !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !SupportedHosts.Contains(uri.IdnHost))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? candidate = null;
        if (uri.IdnHost.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length == 1) candidate = segments[0];
        }
        else if (segments.Length == 1 && segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
        {
            candidate = GetQueryValue(uri.Query, "v");
        }
        else if (segments.Length == 2
            && (segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)))
        {
            candidate = segments[1];
        }

        if (!IsVideoId(candidate)) return false;
        videoId = candidate!;
        return true;
    }

    private static string? GetQueryValue(string query, string key)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0) continue;
            var name = Uri.UnescapeDataString(part[..separator].Replace('+', ' '));
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(part[(separator + 1)..].Replace('+', ' '));
        }

        return null;
    }

    public static bool IsYouTubeHost(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        var host = uri.IdnHost;
        return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsVideoId(string? candidate) =>
        candidate is { Length: 11 }
        && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
