using Microsoft.Data.Sqlite;

namespace TrackMyTime.Web.Data;

/// <summary>Takes consistent point-in-time snapshots of the live database via SQLite's
/// `VACUUM INTO`, which is safe to run against a database that's open (WAL mode) elsewhere in
/// the same process — unlike a raw file copy, it can't capture a half-written page.</summary>
public static class SqliteBackup
{
    public static async Task<string> SnapshotAsync(string label, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsDirectory);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}_{label}.db";
        var destination = Path.Combine(AppPaths.BackupsDirectory, fileName);

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // Parameter binding isn't supported for VACUUM INTO's target; the path is our own
        // generated filename, never user input, so this is safe.
        command.CommandText = $"VACUUM INTO '{destination.Replace("'", "''")}';";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return destination;
    }

    /// <summary>Deletes all but the newest <paramref name="keep"/> snapshots so backups don't
    /// grow unbounded.</summary>
    public static void EnforceRetention(int keep)
    {
        if (!Directory.Exists(AppPaths.BackupsDirectory))
        {
            return;
        }

        var stale = Directory.EnumerateFiles(AppPaths.BackupsDirectory, "*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Skip(keep);

        foreach (var file in stale)
        {
            file.Delete();
        }
    }
}
