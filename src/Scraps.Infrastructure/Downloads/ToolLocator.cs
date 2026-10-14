namespace Scraps.Infrastructure.Downloads;

// Ordem de resolução: caminho explícito (variável de ambiente), depois a pasta third-party/ ao lado do app,
// e por último o nome puro, que deixa o sistema procurar no PATH.
public static class ToolLocator
{
    public static string Resolve(string? overridePath, string baseDirectory, string toolFolder, string toolName)
    {
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;

        var bundled = Path.Combine(baseDirectory, "third-party", toolFolder, ExecutableName(toolName));
        return File.Exists(bundled) ? bundled : ExecutableName(toolName);
    }

    public static string ExecutableName(string toolName) =>
        OperatingSystem.IsWindows() ? $"{toolName}.exe" : toolName;
}
