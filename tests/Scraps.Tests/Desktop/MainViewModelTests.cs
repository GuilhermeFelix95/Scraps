using Scraps.Desktop.ViewModels;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Desktop;

public sealed class MainViewModelTests
{
    [Fact]
    public void New_shell_starts_with_app_title()
    {
        var viewModel = new MainViewModel(_ => throw new InvalidOperationException("Não deve ser chamado."));

        Assert.Equal("Scraps", viewModel.Title);
    }

    [Fact]
    public async Task Initialize_reports_startup_failure_without_creating_new_media()
    {
        var viewModel = new MainViewModel(_ => throw new InvalidOperationException("ferramenta ausente"));

        await viewModel.InitializeAsync();

        Assert.Null(viewModel.NewMedia);
        Assert.Contains("ferramenta ausente", viewModel.StartupMessage);
    }
}
