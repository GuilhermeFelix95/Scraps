using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Scraps.Application.Downloads;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Tests.Tooling;

public sealed class FetchToolsScriptTests
{
    [Fact]
    public async Task Installs_verified_tools_into_output_directory_and_records_versions()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var sources = CreateSources(root.FullName);
            var manifest = WriteManifest(root.FullName, sources, expectedYtDlpHash: Sha256(Path.Combine(sources, "yt-dlp.exe")));
            var output = Path.Combine(root.FullName, "third-party");

            var result = await RunScriptAsync(manifest, output, sources);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(output, "yt-dlp", "yt-dlp.exe")));
            Assert.True(File.Exists(Path.Combine(output, "ffmpeg", "ffmpeg.exe")));
            Assert.True(File.Exists(Path.Combine(output, "ffmpeg", "ffprobe.exe")));
            var versions = File.ReadAllText(Path.Combine(output, "VERSIONS.txt"));
            Assert.Contains("yt-dlp 2026.08.19", versions);
            Assert.Contains("ffmpeg 9.0.2", versions);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Rejects_hash_mismatch_and_writes_nothing()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var sources = CreateSources(root.FullName);
            var manifest = WriteManifest(root.FullName, sources, expectedYtDlpHash: new string('0', 64));
            var output = Path.Combine(root.FullName, "third-party");

            var result = await RunScriptAsync(manifest, output, sources);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("SHA-256 inválido", result.StandardError);
            Assert.False(File.Exists(Path.Combine(output, "yt-dlp", "yt-dlp.exe")));
            Assert.False(File.Exists(Path.Combine(output, "ffmpeg", "ffmpeg.exe")));
        }
        finally { root.Delete(true); }
    }

    private static string CreateSources(string root)
    {
        var sources = Path.Combine(root, "sources");
        Directory.CreateDirectory(sources);
        File.WriteAllText(Path.Combine(sources, "yt-dlp.exe"), "fake yt-dlp");
        using var archive = ZipFile.Open(Path.Combine(sources, "ffmpeg-release-essentials.zip"), ZipArchiveMode.Create);
        WriteEntry(archive, "ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe", "fake ffmpeg");
        WriteEntry(archive, "ffmpeg-9.0.2-essentials_build/bin/ffprobe.exe", "fake ffprobe");
        WriteEntry(archive, "ffmpeg-9.0.2-essentials_build/LICENSE", "license text");
        return sources;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
        writer.Write(content);
    }

    private static string WriteManifest(string root, string sources, string expectedYtDlpHash)
    {
        var manifest = new
        {
            tools = new object[]
            {
                new
                {
                    name = "yt-dlp", version = "2026.08.19", fileName = "yt-dlp.exe",
                    url = "https://example.invalid/yt-dlp.exe", sha256 = expectedYtDlpHash,
                    kind = "binary", outputs = new[] { "yt-dlp.exe" }
                },
                new
                {
                    name = "ffmpeg", version = "9.0.2", fileName = "ffmpeg-release-essentials.zip",
                    url = "https://example.invalid/ffmpeg.zip",
                    sha256 = Sha256(Path.Combine(sources, "ffmpeg-release-essentials.zip")),
                    kind = "zip", outputs = new[] { "ffmpeg.exe", "ffprobe.exe" }
                }
            }
        };
        var path = Path.Combine(root, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
        return path;
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static async Task<ExternalProcessResult> RunScriptAsync(string manifest, string output, string sources)
    {
        var script = Path.Combine(FindRepositoryRoot(), "scripts", "fetch-tools.ps1");
        var runner = new SystemExternalProcessRunner();
        return await runner.RunAsync("pwsh",
            ["-NoProfile", "-File", script,
                "-ManifestPath", manifest, "-OutputDirectory", output, "-LocalSourceDirectory", sources],
            CancellationToken.None);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "fetch-tools.ps1")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("scripts/fetch-tools.ps1 não encontrado.");
    }
}
