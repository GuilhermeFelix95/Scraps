using System.Globalization;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class FfmpegVideoTranscoder(IExternalProcessRunner processRunner, string ffmpegExecutable = "ffmpeg") : IMediaTranscoder
{
    public async Task<string> TranscodeAsync(
        string sourcePath,
        string destinationPath,
        VideoInfo sourceInfo,
        VideoQualityPreset preset,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(sourceInfo);
        cancellationToken.ThrowIfCancellationRequested();
        sourcePath = Path.GetFullPath(sourcePath);
        destinationPath = Path.GetFullPath(destinationPath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("The source media file was not found.", sourcePath);
        if (File.Exists(destinationPath)) throw new IOException("The destination file already exists.");

        if (preset == VideoQualityPreset.Original)
            return await CopyOriginalAsync(sourcePath, destinationPath, progress, cancellationToken);

        var height = GetTargetHeight(preset);
        if (height > sourceInfo.Height)
            throw new InvalidOperationException("The selected quality would upscale the source video.");
        if (!Path.GetExtension(destinationPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Converted video destinations must use the .mp4 extension.", nameof(destinationPath));

        var directory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory,
            $".{Path.GetFileNameWithoutExtension(destinationPath)}.{Guid.NewGuid():N}.partial.mp4");
        try
        {
            var arguments = new[]
            {
                "-hide_banner", "-nostdin", "-n", "-i", sourcePath,
                "-map", "0:v:0", "-map", "0:a?",
                "-c:v", "libx264", "-preset", "medium", "-crf", "23",
                "-vf", $"scale=-2:{height}",
                "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart",
                "-progress", "pipe:1", "-nostats", temporaryPath
            };
            var ffmpegProgress = new InlineProgress<string>(line => ReportProgress(line, sourceInfo.Duration, progress));
            var result = await processRunner.RunAsync(ffmpegExecutable, arguments, cancellationToken, ffmpegProgress);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg failed: {result.StandardError.Trim()}");
            if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                throw new InvalidDataException("FFmpeg completed without creating a video file.");

            File.Move(temporaryPath, destinationPath);
            progress?.Report(new DownloadProgress(0, null, 1));
            return destinationPath;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private static async Task<string> CopyOriginalAsync(
        string sourcePath,
        string destinationPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(directory);
        var outputCreated = false;
        try
        {
            await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            outputCreated = true;
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                total += read;
                progress?.Report(new DownloadProgress(total, input.Length));
            }
            await output.FlushAsync(cancellationToken);
            return destinationPath;
        }
        catch
        {
            if (outputCreated && File.Exists(destinationPath)) File.Delete(destinationPath);
            throw;
        }
    }

    private static int GetTargetHeight(VideoQualityPreset preset) => preset switch
    {
        VideoQualityPreset.P1080 => 1080,
        VideoQualityPreset.P720 => 720,
        VideoQualityPreset.P480 => 480,
        VideoQualityPreset.P360 => 360,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), "The selected video quality is not supported.")
    };

    private static void ReportProgress(string line, TimeSpan duration, IProgress<DownloadProgress>? progress)
    {
        var separator = line.IndexOf('=');
        if (separator < 0) return;
        var key = line[..separator];
        var value = line[(separator + 1)..];
        if (key.Equals("progress", StringComparison.OrdinalIgnoreCase) && value.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new DownloadProgress(0, null, 1));
            return;
        }

        double elapsedSeconds;
        if (key.Equals("out_time", StringComparison.OrdinalIgnoreCase)
            && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var elapsed))
        {
            elapsedSeconds = elapsed.TotalSeconds;
        }
        else if ((key.Equals("out_time_us", StringComparison.OrdinalIgnoreCase)
                  || key.Equals("out_time_ms", StringComparison.OrdinalIgnoreCase))
                 && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
        {
            elapsedSeconds = microseconds / 1_000_000d;
        }
        else
        {
            return;
        }

        if (duration > TimeSpan.Zero && double.IsFinite(elapsedSeconds))
        {
            var fraction = Math.Clamp(elapsedSeconds / duration.TotalSeconds, 0, 0.99);
            progress?.Report(new DownloadProgress(0, null, fraction));
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
