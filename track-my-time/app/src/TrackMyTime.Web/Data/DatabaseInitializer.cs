using System.Reflection;
using DbUp;
using Microsoft.Data.Sqlite;

namespace TrackMyTime.Web.Data;

/// <summary>Runs at startup: snapshots the current database (if one already exists) before
/// applying any pending SQL migrations, so a bad migration is always recoverable from
/// "/data/backups" without having to redeploy an older image.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(ILogger logger, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        if (File.Exists(AppPaths.DatabasePath))
        {
            var snapshotPath = await SqliteBackup.SnapshotAsync("pre-migration", cancellationToken);
            logger.LogInformation("Pre-migration snapshot written to {SnapshotPath}", snapshotPath);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        var upgrader = DeployChanges.To
            .SqliteDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogTo(new MicrosoftLoggerUpgradeLog(logger))
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        logger.LogInformation("Database is up to date ({ScriptCount} script(s) applied this run)",
            result.Scripts.Count());
    }
}
