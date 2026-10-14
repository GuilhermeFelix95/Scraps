namespace Scraps.Application.Downloads;

// Origem já classificada, ainda sem validar o arquivo nem consultar a rede.
public sealed record MediaSourceRequest(MediaSourceKind Kind, string Source, string? YouTubeVideoId = null);
