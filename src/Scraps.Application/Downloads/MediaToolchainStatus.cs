namespace Scraps.Application.Downloads;

// Resultado da checagem feita na inicialização; guia quais fluxos e presets ficam disponíveis.
public sealed record MediaToolchainStatus(bool FfmpegAvailable, bool FfprobeAvailable);
