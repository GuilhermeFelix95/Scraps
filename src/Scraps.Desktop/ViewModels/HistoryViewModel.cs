using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scraps.Application.Downloads;

namespace Scraps.Desktop.ViewModels;

// Histórico paginado por cursor: a UI só devolve o cursor recebido, sem interpretá-lo.
public sealed partial class HistoryViewModel(
    IDownloadHistoryRepository history,
    Action<string> openFolder,
    int pageSize = HistoryViewModel.DefaultPageSize) : ObservableObject
{
    public const int DefaultPageSize = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMore))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    private string? _nextCursor;

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    public bool HasMore => NextCursor is not null;

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var page = await history.GetPageAsync(cursor: null, pageSize, cancellationToken);
        Items.Clear();
        Append(page);
    }

    [RelayCommand(CanExecute = nameof(HasMore))]
    private async Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        var page = await history.GetPageAsync(NextCursor, pageSize, cancellationToken);
        Append(page);
    }

    private void Append(DownloadHistoryPage page)
    {
        foreach (var job in page.Items)
            Items.Add(new HistoryItemViewModel(job, openFolder));
        NextCursor = page.NextCursor;
    }
}
