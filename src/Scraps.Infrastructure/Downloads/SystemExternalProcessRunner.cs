using System.Diagnostics;
using System.Text;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class SystemExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardOutputProgress = null,
        IProgress<string>? standardErrorProgress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException($"Could not start process '{executable}'.");

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        var outputTask = ReadOutputAsync(process.StandardOutput, standardOutput, standardOutputProgress);
        var errorTask = ReadOutputAsync(process.StandardError, standardError, standardErrorProgress);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(outputTask, errorTask);
            throw;
        }

        await Task.WhenAll(outputTask, errorTask);
        return new ExternalProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static async Task ReadOutputAsync(StreamReader reader, StringBuilder output, IProgress<string>? progress)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            output.AppendLine(line);
            try { progress?.Report(line); }
            catch { /* Progress observers must not block process output draining. */ }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
