namespace Scraps.Infrastructure.Downloads;

public sealed class DestinationPathAllocator
{
    public Task<string> AllocateAsync(string directory, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = suggestedFileName.Replace('\\', '/');
        var leaf = Path.GetFileName(normalized);
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safeName = new string(leaf.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "download";
        }

        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(safeName);
        var stem = Path.GetFileNameWithoutExtension(safeName);
        var candidate = Path.Combine(directory, safeName);
        var index = 1;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{stem} ({index++}){extension}");
        }

        return Task.FromResult(candidate);
    }
}
