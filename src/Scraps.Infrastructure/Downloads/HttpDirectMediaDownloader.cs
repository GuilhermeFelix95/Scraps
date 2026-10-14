using System.Net;
using System.Net.Sockets;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class HttpDirectMediaDownloader : IDisposable
{
    private const int MaxRedirects = 5;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly DestinationPathAllocator _pathAllocator;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost;

    public HttpDirectMediaDownloader(
        DestinationPathAllocator pathAllocator,
        HttpMessageHandler? handler = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
    {
        _pathAllocator = pathAllocator ?? throw new ArgumentNullException(nameof(pathAllocator));
        _resolveHost = resolveHost ?? ((host, token) => Dns.GetHostAddressesAsync(host, token));
        handler ??= new HttpClientHandler { AllowAutoRedirect = false };
        _httpClient = new HttpClient(handler, disposeHandler: true);
        _ownsClient = true;
    }

    public async Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var current = source;
        HttpResponseMessage response;
        for (var redirects = 0; ; redirects++)
        {
            await EnsurePublicHttpUriAsync(current, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!IsRedirect(response.StatusCode))
            {
                break;
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || redirects >= MaxRedirects)
            {
                throw new HttpRequestException("The media URL exceeded the redirect limit or returned an invalid redirect.");
            }

            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException($"The URL returned non-media content ({mediaType}).");
            }

            var suggestedName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? Path.GetFileName(current.LocalPath);
            suggestedName = suggestedName?.Trim('"');
            if (string.IsNullOrWhiteSpace(suggestedName))
            {
                suggestedName = "download.bin";
            }

            var destinationPath = await _pathAllocator.AllocateAsync(destinationDirectory, suggestedName, cancellationToken);
            var outputCreated = false;
            try
            {
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                outputCreated = true;

                var buffer = new byte[81920];
                long totalBytes = 0;
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytes += bytesRead;
                    progress?.Report(new DownloadProgress(totalBytes, response.Content.Headers.ContentLength));
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

    private async Task EnsurePublicHttpUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Only absolute HTTP and HTTPS media URLs are accepted.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            addresses = await _resolveHost(uri.DnsSafeHost, cancellationToken);
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivateOrReserved))
        {
            throw new InvalidOperationException("The media URL resolves to a local or non-public network address.");
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC;
        }

        var octets = address.GetAddressBytes();
        return octets[0] == 0
            || octets[0] == 10
            || (octets[0] == 100 && octets[1] is >= 64 and <= 127)
            || (octets[0] == 169 && octets[1] == 254)
            || (octets[0] == 172 && octets[1] is >= 16 and <= 31)
            || (octets[0] == 192 && octets[1] == 168)
            || (octets[0] == 192 && octets[1] == 0)
            || (octets[0] == 198 && octets[1] is 18 or 19)
            || octets[0] >= 224;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
