using CommunityToolkit.Mvvm.ComponentModel;

namespace Scraps.Desktop.ViewModels;

// Casca da janela principal. As telas de nova mídia, fila e histórico entram nas próximas etapas.
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Scraps";
}
