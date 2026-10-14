using Avalonia;
using Avalonia.Controls;
using Scraps.Desktop.ViewModels;

namespace Scraps.Desktop.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    // Carrega a página mais recente ao abrir a aba, para não mostrar uma lista vazia até o usuário clicar em Atualizar.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is HistoryViewModel history)
            history.RefreshCommand.Execute(null);
    }
}
