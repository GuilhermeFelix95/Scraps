using System.Diagnostics;
using Scraps.Infrastructure.Logging;

namespace Scraps.Tests.Logging;

public sealed class AppLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Trace_errors_are_written_to_the_daily_log_file()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var path = AppLog.UseFileIn(root.FullName, Now);
            try
            {
                Trace.TraceError("falha de teste de registro");
                Trace.Flush();

                Assert.Equal(Path.Combine(root.FullName, "scraps-2026-10-14.log"), path);
                // O listener mantém o arquivo aberto para escrita; a leitura precisa aceitar isso, como um editor de texto faria.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                Assert.Contains("falha de teste de registro", reader.ReadToEnd());
            }
            finally
            {
                AppLog.Stop();
            }
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Logs_older_than_the_retention_period_are_removed_and_recent_ones_kept()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var expired = Path.Combine(root.FullName, "scraps-2026-10-05.log");
            var recent = Path.Combine(root.FullName, "scraps-2026-10-12.log");
            File.WriteAllText(expired, "antigo");
            File.WriteAllText(recent, "recente");
            File.SetLastWriteTimeUtc(expired, Now.AddDays(-(AppLog.RetentionDays + 1)).UtcDateTime);
            File.SetLastWriteTimeUtc(recent, Now.AddDays(-2).UtcDateTime);

            AppLog.UseFileIn(root.FullName, Now);
            try
            {
                Assert.False(File.Exists(expired));
                Assert.True(File.Exists(recent));
            }
            finally
            {
                AppLog.Stop();
            }
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Calling_it_twice_does_not_duplicate_the_listener()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            AppLog.UseFileIn(root.FullName, Now);
            AppLog.UseFileIn(root.FullName, Now);
            try
            {
                Assert.Single(Trace.Listeners.Cast<TraceListener>(), listener => listener.Name == AppLog.ListenerName);
            }
            finally
            {
                AppLog.Stop();
            }
        }
        finally
        {
            root.Delete(true);
        }
    }
}
