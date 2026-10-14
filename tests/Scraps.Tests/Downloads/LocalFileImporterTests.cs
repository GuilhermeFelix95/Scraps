using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class LocalFileImporterTests
{
    [Fact]
    public async Task Import_copies_file_contents_to_destination()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            var destination = Path.Combine(root.FullName, "library");
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            await File.WriteAllBytesAsync(source, bytes);
            var importer = new LocalFileImporter(new DestinationPathAllocator());

            var result = await importer.ImportAsync(source, destination);

            Assert.Equal(bytes, await File.ReadAllBytesAsync(result));
            Assert.EndsWith("source.mp4", result);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Import_throws_for_missing_source()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var importer = new LocalFileImporter(new DestinationPathAllocator());

            await Assert.ThrowsAsync<FileNotFoundException>(() => importer.ImportAsync(Path.Combine(root.FullName, "missing.mp4"), root.FullName));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Import_honors_cancellation_before_creating_output()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.mp4");
            await File.WriteAllTextAsync(source, "content");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var destination = Path.Combine(root.FullName, "library");
            var importer = new LocalFileImporter(new DestinationPathAllocator());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importer.ImportAsync(source, destination, cancellationToken: cancellation.Token));
            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
