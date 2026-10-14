using Scraps.Desktop.ViewModels;

namespace Scraps.Tests.Desktop;

public sealed class MainViewModelTests
{
    [Fact]
    public void New_shell_starts_with_app_title()
    {
        var viewModel = new MainViewModel();

        Assert.Equal("Scraps", viewModel.Title);
    }
}
