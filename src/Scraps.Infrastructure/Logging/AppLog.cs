using System.Diagnostics;
using System.Globalization;
using Scraps.Infrastructure.Downloads;

namespace Scraps.Infrastructure.Logging;

// Registro em arquivo diário das falhas que o app já reporta via Trace. Um arquivo por dia, retenção de 7 dias.
public static class AppLog
{
    public const int RetentionDays = 7;
    public const string ListenerName = "scraps-arquivo";

    public static string DefaultDirectory => Path.Combine(
        Path.GetDirectoryName(DatabaseInitializer.GetDefaultDatabasePath())!,
        "logs");

    public static void UseDefault() => UseFileIn(DefaultDirectory, DateTimeOffset.Now);

    // Devolve o caminho do arquivo do dia. Chamar de novo troca o listener em vez de duplicá-lo.
    public static string UseFileIn(string directory, DateTimeOffset now)
    {
        Directory.CreateDirectory(directory);
        RemoveExpired(directory, now);
        Stop();

        var path = Path.Combine(directory, $"scraps-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");
        var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
        Trace.Listeners.Add(new TextWriterTraceListener(writer) { Name = ListenerName });
        return path;
    }

    public static void Stop()
    {
        for (var index = Trace.Listeners.Count - 1; index >= 0; index--)
        {
            if (Trace.Listeners[index].Name != ListenerName) continue;
            Trace.Listeners[index].Dispose();
            Trace.Listeners.RemoveAt(index);
        }
    }

    private static void RemoveExpired(string directory, DateTimeOffset now)
    {
        var cutoff = now.AddDays(-RetentionDays).UtcDateTime;
        foreach (var file in Directory.EnumerateFiles(directory, "scraps-*.log"))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
        }
    }
}
