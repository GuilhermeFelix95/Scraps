namespace Scraps.Application.Downloads;

public interface IVideoProbe
{
    Task<VideoInfo> ProbeAsync(string path, CancellationToken cancellationToken = default);
}
