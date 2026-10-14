using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class DestinationPathAllocatorTests
{
    [Fact]
    public async Task Allocate_uses_suggested_name_when_available()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var allocator = new DestinationPathAllocator();
            var path = await allocator.AllocateAsync(directory.FullName, "video.mp4");

            Assert.Equal(Path.Combine(directory.FullName, "video.mp4"), path);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task Allocate_adds_suffix_instead_of_overwriting_existing_file()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "video.mp4"), "keep");
            var allocator = new DestinationPathAllocator();

            var path = await allocator.AllocateAsync(directory.FullName, "video.mp4");

            Assert.Equal(Path.Combine(directory.FullName, "video (1).mp4"), path);
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(directory.FullName, "video.mp4")));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Theory]
    [InlineData("../../outside.mp4", "outside.mp4")]
    [InlineData("..\\..\\outside.mp4", "outside.mp4")]
    public async Task Allocate_strips_directory_components(string input, string expected)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var allocator = new DestinationPathAllocator();

            var path = await allocator.AllocateAsync(directory.FullName, input);

            Assert.Equal(Path.Combine(directory.FullName, expected), path);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
