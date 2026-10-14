using System.ComponentModel;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class MediaToolchainChecker(
    IExternalProcessRunner processRunner,
    string ffmpegExecutable = "ffmpeg",
    string ffprobeExecutable = "ffprobe")
{
    public async Task<MediaToolchainStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var ffmpegAvailable = await IsAvailableAsync(ffmpegExecutable, cancellationToken);
        var ffprobeAvailable = await IsAvailableAsync(ffprobeExecutable, cancellationToken);
        return new MediaToolchainStatus(ffmpegAvailable, ffprobeAvailable);
    }

    private async Task<bool> IsAvailableAsync(string executable, CancellationToken cancellationToken)
    {
        try
        {
            var result = await processRunner.RunAsync(executable, ["-version"], cancellationToken);
            return result.ExitCode == 0;
        }
        // Executável ausente do PATH: o Windows reporta ERROR_FILE_NOT_FOUND (2) ou ERROR_PATH_NOT_FOUND (3).
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return false;
        }
    }
}
