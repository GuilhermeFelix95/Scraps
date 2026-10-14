using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Desktop.ViewModels;

// Casca da janela principal: cria os serviços em segundo plano e expõe as telas.
public sealed partial class MainViewModel(Func<CancellationToken, Task<ScrapsServices>> createServices) : ObservableObject, IAsyncDisposable
{
    private ScrapsServices? _services;

    [ObservableProperty]
    private string _title = "Scraps";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartupMessage))]
    private string? _startupMessage = "Preparando ferramentas...";

    [ObservableProperty]
    private NewMediaViewModel? _newMedia;

    [ObservableProperty]
    private QueueViewModel? _queue;

    [ObservableProperty]
    private HistoryViewModel? _history;

    [ObservableProperty]
    private ToolsViewModel? _tools;

    public bool HasStartupMessage => StartupMessage is not null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var services = await createServices(cancellationToken);
            await services.Coordinator.StartAsync(cancellationToken);
            _services = services;
            // Progresso chega da thread do worker; a fila o leva à thread de interface.
            var queue = new QueueViewModel(services.Coordinator, action => Dispatcher.UIThread.Post(action));
            Queue = queue;
            History = new HistoryViewModel(services.History, OpenFolder);
            Tools = new ToolsViewModel(services.Toolchain, services.Updater, services.YtDlpExecutable);
            // Como na CLI, a atualização do yt-dlp (com limite de tempo) termina antes de liberar a nova mídia:
            // nenhuma consulta ou download usa o binário enquanto ele é trocado.
            await Tools.CheckYtDlpAsync(cancellationToken);
            NewMedia = new NewMediaViewModel(services.Import, services.Coordinator, DefaultDownloadDirectory.GetPath(), queue.Track);
            StartupMessage = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            StartupMessage = $"Não foi possível iniciar: {exception.Message}";
        }
    }

    // Abre o explorador do sistema na pasta, sem montar comando de shell.
    private static void OpenFolder(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    public async ValueTask DisposeAsync()
    {
        Queue?.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }
}
