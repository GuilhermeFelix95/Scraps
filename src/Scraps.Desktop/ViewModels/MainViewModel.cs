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

    public bool HasStartupMessage => StartupMessage is not null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var services = await createServices(cancellationToken);
            await services.Coordinator.StartAsync(cancellationToken);
            _services = services;
            NewMedia = new NewMediaViewModel(services.Import, services.Coordinator, DefaultDownloadDirectory.GetPath());
            StartupMessage = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            StartupMessage = $"Não foi possível iniciar: {exception.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
    }
}
