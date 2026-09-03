namespace TrackMyTime.Web.Data;

/// <summary>Takes a consistent daily snapshot of the database independent of whenever HA
/// happens to run its own backup — protects against HA's backup catching the DB mid-write, and
/// gives you point-in-time recovery beyond just "before the last migration".</summary>
public sealed class DailyBackupService(ILogger<DailyBackupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private const int SnapshotsToKeep = 14;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var path = await SqliteBackup.SnapshotAsync("daily", stoppingToken);
                SqliteBackup.EnforceRetention(SnapshotsToKeep);
                logger.LogInformation("Daily backup snapshot written to {Path}", path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Daily backup snapshot failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
