using System.Net;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Downloads;

public class HttpDirectMediaDownloaderTests
{
    [Fact]
    public async Task Download_streams_response_to_unique_file()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 9, 8, 7 })
            });
            using var downloader = new HttpDirectMediaDownloader(
                new DestinationPathAllocator(), handler,
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

            var path = await downloader.DownloadAsync(new Uri("https://media.example/video.mp4"), root.FullName);

            Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(path));
            Assert.EndsWith("video.mp4", path);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Download_revalidates_redirects_and_blocks_loopback()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("http://127.0.0.1/private.mp4") }
            });
            using var downloader = new HttpDirectMediaDownloader(
                new DestinationPathAllocator(), handler,
                (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(new Uri("https://media.example/video.mp4"), root.FullName));

            Assert.Equal(1, handler.RequestCount);
            Assert.Empty(Directory.EnumerateFiles(root.FullName));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Download_rejects_non_http_schemes()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            using var downloader = new HttpDirectMediaDownloader(new DestinationPathAllocator());

            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(new Uri("file:///C:/secret.mp4"), root.FullName));
        }
        finally
        {
            root.Delete(true);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
