namespace Scraps.Application.Downloads;

public static class MediaSourceParser
{
    public static MediaSourceRequest Parse(string rawSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSource);
        var source = rawSource.Trim();

        if (YouTubeLinkParser.TryParse(source, out var videoId))
            return new MediaSourceRequest(MediaSourceKind.YouTube, source, videoId);
        // Páginas do YouTube que não são de um vídeo único não viram arquivo local nem URL direta.
        if (YouTubeLinkParser.IsYouTubeHost(source))
            throw new MediaSourceException("Link do YouTube inválido ou não suportado.");
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return new MediaSourceRequest(MediaSourceKind.DirectUrl, source);
        return new MediaSourceRequest(MediaSourceKind.LocalFile, source);
    }
}
