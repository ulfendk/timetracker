using Dapper;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Models;
using TrackMyTime.Web.Models.Export;
using TrackMyTime.Web.Repositories;

namespace TrackMyTime.Web.Services;

public enum ConflictResolution
{
    Skip,
    Overwrite,
}

/// <summary>Dates that already have data in the target database, so the import UI can show them
/// and require an explicit Skip/Overwrite choice before anything is written.</summary>
public sealed record ImportPreview(
    IReadOnlyList<DateOnly> ConflictingTimeEntryDates,
    IReadOnlyList<DateOnly> ConflictingDayOffDates,
    int NewClientCount,
    int NewProjectCount,
    int TotalIncomingTimeEntries,
    int TotalIncomingDaysOff);

public sealed record ImportResult(
    int TimeEntriesImported, int TimeEntriesSkipped,
    int DaysOffImported, int DaysOffSkipped,
    int NominalHoursSettingsImported);

/// <summary>Exports/imports Track My Time's own data as a self-contained JSON document - not a
/// general importer for arbitrary third-party formats. Used both for backup/restore and as the
/// target shape a one-off conversion script can produce from other sources.</summary>
public sealed class ExportImportService(
    IClientRepository clientRepository,
    IProjectRepository projectRepository,
    ITimeEntryRepository timeEntryRepository,
    IDayOffRepository dayOffRepository,
    INominalHoursRepository nominalHoursRepository,
    SqliteConnectionFactory connectionFactory)
{
    public async Task<ExportDocument> ExportAsync()
    {
        var clients = await clientRepository.GetAllAsync(includeInactive: true);
        var projects = await projectRepository.GetAllAsync(includeInactive: true);
        var timeEntries = await timeEntryRepository.GetByDateRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var daysOff = await dayOffRepository.GetByDateRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var nominalHours = await nominalHoursRepository.GetAllAsync();

        return new ExportDocument
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Clients = clients.Select(c => new ExportClient { Id = c.Id, Name = c.Name, IsActive = c.IsActive }).ToList(),
            Projects = projects.Select(p => new ExportProject
            {
                Id = p.Id, ClientId = p.ClientId, Name = p.Name, IsActive = p.IsActive, Color = p.Color,
            }).ToList(),
            TimeEntries = timeEntries.Select(t => new ExportTimeEntry
            {
                Id = t.Id, Date = t.Date, ProjectId = t.ProjectId, DurationMinutes = t.DurationMinutes, Note = t.Note,
                StartTime = t.StartTime, EndTime = t.EndTime, BreakMinutes = t.BreakMinutes,
            }).ToList(),
            DaysOff = daysOff.Select(d => new ExportDayOff { Id = d.Id, Date = d.Date, Note = d.Note, Type = d.Type }).ToList(),
            NominalHoursSettings = nominalHours.Select(n => new ExportNominalHoursSetting
            {
                Id = n.Id, EffectiveFrom = n.EffectiveFrom, WeeklyHours = n.WeeklyHours,
            }).ToList(),
        };
    }

    /// <summary>Dry run: never writes anything. Reports which incoming dates already have data
    /// in the target database, so the import UI can require an explicit choice before ApplyAsync
    /// is ever called.</summary>
    public async Task<ImportPreview> BuildPreviewAsync(ExportDocument doc)
    {
        var incomingTimeEntryDates = doc.TimeEntries.Select(t => t.Date).Distinct().ToList();
        var incomingDayOffDates = doc.DaysOff.Select(d => d.Date).Distinct().ToList();

        var allDates = incomingTimeEntryDates.Concat(incomingDayOffDates).ToList();
        IReadOnlyList<DateOnly> conflictingTimeEntryDates = [];
        IReadOnlyList<DateOnly> conflictingDayOffDates = [];

        if (allDates.Count > 0)
        {
            var minDate = allDates.Min();
            var maxDate = allDates.Max();

            var existingTimeEntryDates = (await timeEntryRepository.GetByDateRangeAsync(minDate, maxDate))
                .Select(t => t.Date).ToHashSet();
            var existingDayOffDates = (await dayOffRepository.GetByDateRangeAsync(minDate, maxDate))
                .Select(d => d.Date).ToHashSet();

            conflictingTimeEntryDates = incomingTimeEntryDates.Where(existingTimeEntryDates.Contains).OrderBy(d => d).ToList();
            conflictingDayOffDates = incomingDayOffDates.Where(existingDayOffDates.Contains).OrderBy(d => d).ToList();
        }

        var existingClientNames = (await clientRepository.GetAllAsync(includeInactive: true))
            .Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingProjectKeys = (await projectRepository.GetAllAsync(includeInactive: true))
            .Select(p => (p.ClientName, p.Name))
            .ToHashSet();

        var clientNameById = doc.Clients.ToDictionary(c => c.Id, c => c.Name);
        var newClientCount = doc.Clients.Count(c => !existingClientNames.Contains(c.Name));
        var newProjectCount = doc.Projects.Count(p =>
            !existingProjectKeys.Contains((clientNameById.GetValueOrDefault(p.ClientId, ""), p.Name)));

        return new ImportPreview(
            conflictingTimeEntryDates, conflictingDayOffDates,
            newClientCount, newProjectCount,
            doc.TimeEntries.Count, doc.DaysOff.Count);
    }

    /// <summary>Applies the document inside a single transaction, taking a "pre-import" snapshot
    /// first. Clients/projects are matched by name (re-importing your own export shouldn't
    /// duplicate them); TimeEntry/DayOff conflicts are resolved per the caller's explicit
    /// choice - there is no silent default.</summary>
    public async Task<ImportResult> ApplyAsync(ExportDocument doc, ConflictResolution resolution, CancellationToken cancellationToken = default)
    {
        await SqliteBackup.SnapshotAsync("pre-import", cancellationToken);

        using var connection = connectionFactory.Open();
        using var transaction = connection.BeginTransaction();

        var clientIdMap = new Dictionary<int, int>();
        foreach (var client in doc.Clients)
        {
            var existingId = await connection.ExecuteScalarAsync<int?>(
                "SELECT Id FROM Client WHERE Name = @Name COLLATE NOCASE", new { client.Name }, transaction);
            if (existingId is int id)
            {
                clientIdMap[client.Id] = id;
            }
            else
            {
                var newId = await connection.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO Client (Name, IsActive) VALUES (@Name, @IsActive);
                    SELECT last_insert_rowid();
                    """, client, transaction);
                clientIdMap[client.Id] = (int)newId;
            }
        }

        var projectIdMap = new Dictionary<int, int>();
        foreach (var project in doc.Projects)
        {
            var dbClientId = clientIdMap[project.ClientId];
            var existingId = await connection.ExecuteScalarAsync<int?>(
                "SELECT Id FROM Project WHERE ClientId = @dbClientId AND Name = @Name COLLATE NOCASE",
                new { dbClientId, project.Name }, transaction);
            if (existingId is int id)
            {
                projectIdMap[project.Id] = id;
            }
            else
            {
                var newId = await connection.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO Project (ClientId, Name, IsActive, Color) VALUES (@dbClientId, @Name, @IsActive, @Color);
                    SELECT last_insert_rowid();
                    """, new { dbClientId, project.Name, project.IsActive, project.Color }, transaction);
                projectIdMap[project.Id] = (int)newId;
            }
        }

        var nominalImported = 0;
        foreach (var setting in doc.NominalHoursSettings)
        {
            var existingId = await connection.ExecuteScalarAsync<int?>(
                "SELECT Id FROM NominalHoursSetting WHERE EffectiveFrom = @EffectiveFrom",
                new { setting.EffectiveFrom }, transaction);
            if (existingId is int id)
            {
                if (resolution == ConflictResolution.Overwrite)
                {
                    await connection.ExecuteAsync(
                        "UPDATE NominalHoursSetting SET WeeklyHours = @WeeklyHours WHERE Id = @id",
                        new { setting.WeeklyHours, id }, transaction);
                    nominalImported++;
                }
            }
            else
            {
                await connection.ExecuteAsync(
                    "INSERT INTO NominalHoursSetting (EffectiveFrom, WeeklyHours) VALUES (@EffectiveFrom, @WeeklyHours)",
                    setting, transaction);
                nominalImported++;
            }
        }

        var daysOffImported = 0;
        var daysOffSkipped = 0;
        foreach (var dayOff in doc.DaysOff)
        {
            var existingId = await connection.ExecuteScalarAsync<int?>(
                "SELECT Id FROM DayOff WHERE Date = @Date", new { dayOff.Date }, transaction);
            if (existingId is int id)
            {
                if (resolution == ConflictResolution.Skip)
                {
                    daysOffSkipped++;
                    continue;
                }
                await connection.ExecuteAsync(
                    "UPDATE DayOff SET Note = @Note, Type = @Type WHERE Id = @id",
                    new { dayOff.Note, dayOff.Type, id }, transaction);
            }
            else
            {
                await connection.ExecuteAsync(
                    "INSERT INTO DayOff (Date, Note, Type) VALUES (@Date, @Note, @Type)", dayOff, transaction);
            }
            daysOffImported++;
        }

        var timeEntriesImported = 0;
        var timeEntriesSkipped = 0;
        foreach (var group in doc.TimeEntries.GroupBy(t => t.Date))
        {
            var date = group.Key;
            var existingCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM TimeEntry WHERE Date = @date", new { date }, transaction);
            if (existingCount > 0)
            {
                if (resolution == ConflictResolution.Skip)
                {
                    timeEntriesSkipped += group.Count();
                    continue;
                }
                await connection.ExecuteAsync("DELETE FROM TimeEntry WHERE Date = @date", new { date }, transaction);
            }

            foreach (var entry in group)
            {
                var dbProjectId = projectIdMap[entry.ProjectId];
                await connection.ExecuteAsync(
                    """
                    INSERT INTO TimeEntry (Date, ProjectId, DurationMinutes, Note, StartTime, EndTime, BreakMinutes)
                    VALUES (@Date, @dbProjectId, @DurationMinutes, @Note, @StartTime, @EndTime, @BreakMinutes)
                    """,
                    new
                    {
                        entry.Date, dbProjectId, entry.DurationMinutes, entry.Note,
                        entry.StartTime, entry.EndTime, entry.BreakMinutes,
                    }, transaction);
                timeEntriesImported++;
            }
        }

        transaction.Commit();

        return new ImportResult(timeEntriesImported, timeEntriesSkipped, daysOffImported, daysOffSkipped, nominalImported);
    }
}
