using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Opção de qualidade já com o rótulo pronto, para a interface não precisar de conversor.
public sealed record PresetOption(VideoQualityPreset Preset, string Label);
