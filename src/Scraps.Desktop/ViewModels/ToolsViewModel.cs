using CommunityToolkit.Mvvm.ComponentModel;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Desktop.ViewModels;

// Painel de ferramentas: mostra o que está disponível, os mesmos avisos da CLI e o resultado da atualização do yt-dlp.
public sealed partial class ToolsViewModel(MediaToolchainStatus toolchain, YtDlpUpdater updater, string ytDlpExecutable) : ObservableObject
{
    public string FfmpegStatus { get; } = Availability(toolchain.FfmpegAvailable);

    public string FfprobeStatus { get; } = Availability(toolchain.FfprobeAvailable);

    public IReadOnlyList<string> Warnings { get; } = ToolchainWarnings.From(toolchain);

    [ObservableProperty]
    private string _ytDlpStatus = "Verificando atualização do yt-dlp...";

    public async Task CheckYtDlpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await updater.UpdateAsync(ytDlpExecutable, cancellationToken);
            // Mesma frase da CLI quando a atualização falha; o resto é a mensagem já pronta do updater.
            YtDlpStatus = result.Status == ToolUpdateStatus.Failed
                ? UpdateFailedText(result.Message)
                : result.Message;
        }
        // Falhas fora da lista tratada pelo updater não podem deixar a nova mídia bloqueada: viram um status visível.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            YtDlpStatus = UpdateFailedText(exception.Message);
        }
    }

    private static string UpdateFailedText(string reason) =>
        $"Não foi possível atualizar o yt-dlp ({reason}). Usando a versão instalada.";

    private static string Availability(bool available) => available ? "Disponível" : "Não encontrado";
}
