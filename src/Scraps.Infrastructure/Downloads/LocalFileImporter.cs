using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class LocalFileImporter(DestinationPathAllocator pathAllocator)
{
    public async Task<string> ImportAsync(
        string sourceFilePath,
        string destinationDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("The source file was not found.", sourceFilePath);
        }

        var destinationPath = await pathAllocator.AllocateAsync(
            destinationDirectory,
            Path.GetFileName(sourceFilePath),
            cancellationToken);
        var outputCreated = false;

        try
        {
            await using var input = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            outputCreated = true;

            var buffer = new byte[81920];
            long totalBytes = 0;
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytes += bytesRead;
                progress?.Report(new DownloadProgress(totalBytes, input.Length));
            }

            await output.FlushAsync(cancellationToken);
            return destinationPath;
        }
        catch
        {
            if (outputCreated && File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }
}
