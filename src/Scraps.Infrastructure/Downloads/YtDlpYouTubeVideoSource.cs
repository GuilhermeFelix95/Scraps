using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Scraps.Application.Downloads;

namespace Scraps.Infrastructure.Downloads;

public sealed class YtDlpYouTubeVideoSource : IYouTubeVideoSource
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".avi", ".flv", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".ts", ".webm"
    };

    private const int MaxStreamDetailLength = 1500;
    private const string ProgressMarker = "SCRAPS_PROGRESS:";

    private readonly IExternalProcessRunner _processRunner;
    private readonly string _executable;
    private readonly string? _javaScriptRuntime;
    // Sem ffmpeg o yt-dlp não mescla vídeo e áudio; nesse caso pedimos um formato único pronto.
    private readonly bool _mergeAvailable;

    public YtDlpYouTubeVideoSource(
        IExternalProcessRunner processRunner,
        string executable = "yt-dlp",
        string? javaScriptRuntime = null,
        bool mergeAvailable = true)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (!string.IsNullOrWhiteSpace(javaScriptRuntime)
            && !new[] { "node", "deno", "quickjs" }.Contains(javaScriptRuntime.Split(':', 2)[0], StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use um runtime suportado: node, deno ou quickjs.", nameof(javaScriptRuntime));
        _executable = executable;
        _javaScriptRuntime = string.IsNullOrWhiteSpace(javaScriptRuntime) ? null : javaScriptRuntime;
        _mergeAvailable = mergeAvailable;
    }

    public async Task<YouTubeVideoMetadata> GetMetadataAsync(Uri source, CancellationToken cancellationToken)
    {
        var videoId = ValidateSource(source);
        var arguments = ConfigureJavaScriptRuntime([
            "--ignore-config", "--no-playlist", "--xff", "never", "--no-warnings",
            "--dump-single-json", "--skip-download", source.AbsoluteUri
        ]);
        var result = await RunAsync(arguments, cancellationToken);
        EnsureSuccess(result, "consultar o vídeo");

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            return ParseMetadata(document.RootElement, videoId);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("yt-dlp retornou metadados de vídeo inválidos.", exception);
        }
    }

    public async Task<string> DownloadAsync(
        Uri source,
        string expectedVideoId,
        string stagingDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        var videoId = ValidateSource(source);
        if (!string.Equals(videoId, expectedVideoId, StringComparison.Ordinal))
            throw new ArgumentException("O ID do vídeo não corresponde à URL fornecida.", nameof(expectedVideoId));

        stagingDirectory = Path.GetFullPath(stagingDirectory);
        Directory.CreateDirectory(stagingDirectory);
        if (Directory.EnumerateFileSystemEntries(stagingDirectory).Any())
            throw new InvalidOperationException("O diretório de staging precisa estar vazio.");

        var outputTemplate = Path.Combine(stagingDirectory, $"{videoId}.%(ext)s");
        var arguments = ConfigureJavaScriptRuntime([
            "--ignore-config", "--no-playlist", "--xff", "never", "--newline",
            "--progress-template", $"download:{ProgressMarker}%(progress._percent_str)s",
            .. FormatArguments(),
            "--no-overwrites", "--output", outputTemplate, source.AbsoluteUri
        ]);
        var stderrProgress = new InlineProgress<string>(line => ReportDownloadProgress(line, progress));
        try
        {
            var result = await RunAsync(arguments, cancellationToken, standardErrorProgress: stderrProgress);
            EnsureSuccess(result, "baixar o vídeo");

            var videoPaths = Directory.EnumerateFiles(stagingDirectory, $"{videoId}.*", SearchOption.TopDirectoryOnly)
                .Where(path => VideoExtensions.Contains(Path.GetExtension(path)))
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), videoId, StringComparison.Ordinal))
                .ToArray();
            if (videoPaths.Length != 1 || !File.Exists(videoPaths[0]) || new FileInfo(videoPaths[0]).Length == 0)
                throw CreateMissingOutputException(result, stagingDirectory, videoId);

            progress?.Report(new DownloadProgress(0, null, 1));
            return videoPaths[0];
        }
        catch
        {
            if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, recursive: true);
            throw;
        }
    }

    private string[] FormatArguments() => _mergeAvailable
        ? ["--format", "bestvideo+bestaudio/best", "--merge-output-format", "mkv"]
        : ["--format", "best[ext=mp4]/best"];

    private static InvalidDataException CreateMissingOutputException(
        ExternalProcessResult result, string stagingDirectory, string videoId)
    {
        string[] entries;
        try
        {
            entries = Directory.Exists(stagingDirectory)
                ? Directory.EnumerateFileSystemEntries(stagingDirectory)
                    .Select(path => Path.GetFileName(path) ?? path)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            entries = [$"<falha ao listar a pasta: {exception.Message}>"];
        }

        // Cada fluxo tem limite próprio: progresso no stdout não pode empurrar o stderr (avisos do yt-dlp) para fora da mensagem.
        var stdout = Truncate(WithoutProgressLines(result.StandardOutput), MaxStreamDetailLength);
        var stderr = Truncate(result.StandardError.Trim(), MaxStreamDetailLength);
        var output = string.Join(Environment.NewLine, new[]
            {
                string.IsNullOrWhiteSpace(stdout) ? null : $"stdout: {stdout}",
                string.IsNullOrWhiteSpace(stderr) ? null : $"stderr: {stderr}"
            }
            .Where(line => line is not null));
        if (string.IsNullOrWhiteSpace(output)) output = "(nenhuma saída capturada)";

        var entryList = entries.Length == 0 ? "(nenhuma)" : string.Join(", ", entries);
        var mergeHint = CountSplitMediaFiles(entries, videoId) > 1
            ? " As faixas de vídeo e áudio foram baixadas separadamente e não foram mescladas: verifique se o ffmpeg está instalado e acessível pelo PATH."
            : string.Empty;
        return new InvalidDataException(
            $"yt-dlp terminou com código {result.ExitCode}, mas não foi encontrado exatamente um arquivo de vídeo não vazio para o ID '{videoId}'.{mergeHint} " +
            $"Staging: '{stagingDirectory}'. Entradas encontradas: {entryList}. Saída do processo:{Environment.NewLine}{output}");
    }

    private static int CountSplitMediaFiles(string[] entries, string videoId) =>
        entries.Count(name => name.StartsWith($"{videoId}.", StringComparison.Ordinal)
            && VideoExtensions.Contains(Path.GetExtension(name)));

    private static string WithoutProgressLines(string standardOutput) =>
        string.Join('\n', standardOutput.Split('\n').Where(line => !line.Contains(ProgressMarker, StringComparison.Ordinal))).Trim();

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : $"{text[..maxLength]}… [saída truncada]";

    private string[] ConfigureJavaScriptRuntime(string[] arguments) =>
        _javaScriptRuntime is null
            ? arguments
            : ["--no-js-runtimes", "--js-runtimes", _javaScriptRuntime, .. arguments];

    private async Task<ExternalProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardErrorProgress = null)
    {
        try
        {
            return await _processRunner.RunAsync(_executable, arguments, cancellationToken,
                standardErrorProgress: standardErrorProgress);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            throw new InvalidOperationException(
                "yt-dlp não foi encontrado. Instale-o separadamente ou configure a variável SCRAPS_YTDLP.", exception);
        }
    }

    private static string ValidateSource(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsAbsoluteUri || source.Scheme != Uri.UriSchemeHttps
            || !YouTubeLinkParser.TryParse(source.AbsoluteUri, out var videoId))
        {
            throw new ArgumentException("Informe uma URL HTTPS válida de vídeo do YouTube.", nameof(source));
        }
        return videoId;
    }

    private static YouTubeVideoMetadata ParseMetadata(JsonElement root, string expectedVideoId)
    {
        var returnedId = GetString(root, "id");
        if (!string.IsNullOrWhiteSpace(returnedId) && !string.Equals(returnedId, expectedVideoId, StringComparison.Ordinal))
            throw new InvalidDataException("O vídeo retornado pelo yt-dlp não corresponde ao ID da URL.");

        var bestWidth = 0;
        var bestHeight = 0;
        if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var format in formats.EnumerateArray())
            {
                var videoCodec = GetString(format, "vcodec");
                if (string.Equals(videoCodec, "none", StringComparison.OrdinalIgnoreCase)) continue;
                var width = GetInt32(format, "width");
                var height = GetInt32(format, "height");
                if (width > 0 && height > bestHeight)
                {
                    bestWidth = width;
                    bestHeight = height;
                }
            }
        }

        if (bestWidth == 0 || bestHeight == 0)
        {
            bestWidth = GetInt32(root, "width");
            bestHeight = GetInt32(root, "height");
        }
        if (bestWidth <= 0 || bestHeight <= 0)
            throw new InvalidDataException("Não foi possível determinar a resolução de vídeo do YouTube.");

        var seconds = GetDouble(root, "duration");
        var duration = seconds is > 0 and <= 315_537_897_599d
            ? TimeSpan.FromSeconds(seconds.Value)
            : TimeSpan.Zero;
        var title = GetString(root, "title");
        return new YouTubeVideoMetadata(expectedVideoId, string.IsNullOrWhiteSpace(title) ? expectedVideoId : title,
            new VideoInfo(bestWidth, bestHeight, duration));
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt32(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static double? GetDouble(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : null;

    private static void EnsureSuccess(ExternalProcessResult result, string operation)
    {
        if (result.ExitCode == 0) return;
        var details = string.IsNullOrWhiteSpace(result.StandardError) ? "sem detalhes" : result.StandardError.Trim();
        if (details.Length > 1500) details = details[..1500];
        var javaScriptIssue = details.Contains("javascript", StringComparison.OrdinalIgnoreCase)
            || details.Contains("js runtime", StringComparison.OrdinalIgnoreCase)
            || details.Contains("yt-dlp-ejs", StringComparison.OrdinalIgnoreCase)
            || details.Contains("challenge", StringComparison.OrdinalIgnoreCase);
        var hint = javaScriptIssue
            ? " Verifique o componente yt-dlp-ejs e um runtime JavaScript suportado; configure SCRAPS_YTDLP_JS_RUNTIME=node se necessário."
            : string.Empty;
        throw new InvalidOperationException($"yt-dlp falhou ao {operation}: {details}.{hint}");
    }

    private static void ReportDownloadProgress(string line, IProgress<DownloadProgress>? progress)
    {
        var markerIndex = line.IndexOf(ProgressMarker, StringComparison.Ordinal);
        if (markerIndex < 0) return;
        var percentageText = line[(markerIndex + ProgressMarker.Length)..].Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        if (!double.TryParse(percentageText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage)
            || !double.IsFinite(percentage)) return;
        progress?.Report(new DownloadProgress(0, null, Math.Clamp(percentage / 100, 0, 0.99)));
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
