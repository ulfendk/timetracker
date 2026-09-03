using System.Globalization;
using System.Text.Json;
using TrackMyTime.Web.Models;
using TrackMyTime.Web.Models.Export;
using TrackMyTime.Web.Models.Legacy;

namespace TrackMyTime.Web.Services;

/// <summary>Converts a <see cref="LegacyExport"/> (a third-party time tracker's export) into
/// Track My Time's own <see cref="ExportDocument"/> shape, scoped to a date range and assigned
/// to one existing client/project. Purely in-memory - the result is meant to be fed straight
/// into ExportImportService.BuildPreviewAsync/ApplyAsync, the same path a normal import takes,
/// so conflict handling is never duplicated.</summary>
public static class LegacyImportConverter
{
    private static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    public static LegacyExport Parse(string json) =>
        JsonSerializer.Deserialize<LegacyExport>(json, ParseOptions)
        ?? throw new InvalidOperationException("The file didn't contain a recognizable export.");

    /// <summary>The full date span covered by the file's "dates" keys - used to constrain the
    /// date-range picker so it can't be set outside what the file actually contains.</summary>
    public static (DateOnly Min, DateOnly Max)? GetDateRange(LegacyExport export)
    {
        var dates = export.Dates.Keys
            .Select(key => DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateOnly?)null)
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToList();

        return dates.Count == 0 ? null : (dates.Min(), dates.Max());
    }

    public static ExportDocument Convert(LegacyExport export, DateOnly from, DateOnly to, string clientName, string projectName)
    {
        var timeEntries = new List<ExportTimeEntry>();
        var daysOff = new List<ExportDayOff>();
        var nominalSettings = new List<ExportNominalHoursSetting>();
        var nextTimeEntryId = 1;
        var nextDayOffId = 1;
        var nextNominalId = 1;

        foreach (var (dateKey, entry) in export.Dates)
        {
            if (!DateOnly.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }
            if (date < from || date > to)
            {
                continue;
            }

            if (entry.Intervals.Count == 0 && !entry.IsHoliday)
            {
                continue;
            }

            foreach (var interval in entry.Intervals)
            {
                if (!TimeOnly.TryParseExact(interval.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                    !TimeOnly.TryParseExact(interval.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                {
                    continue;
                }

                var durationMinutes = (int)(end - start).TotalMinutes - interval.Break;
                if (durationMinutes <= 0)
                {
                    continue;
                }

                timeEntries.Add(new ExportTimeEntry
                {
                    Id = nextTimeEntryId++,
                    Date = date,
                    ProjectId = 1,
                    DurationMinutes = durationMinutes,
                    StartTime = start,
                    EndTime = end,
                    BreakMinutes = interval.Break,
                });
            }

            if (entry.IsHoliday)
            {
                daysOff.Add(new ExportDayOff
                {
                    Id = nextDayOffId++,
                    Date = date,
                    Note = entry.HolidayName,
                    Type = entry.AutoMarked ? DayOffType.OfficialHoliday : DayOffType.Vacation,
                });
            }
        }

        foreach (var (dateKey, weeklyHours) in export.WeeklyExpectedHours)
        {
            if (!DateOnly.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }
            if (date < from || date > to)
            {
                continue;
            }
            nominalSettings.Add(new ExportNominalHoursSetting { Id = nextNominalId++, EffectiveFrom = date, WeeklyHours = weeklyHours });
        }

        return new ExportDocument
        {
            SchemaVersion = 1,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            // Client/Project matching in ExportImportService.ApplyAsync is purely by NAME, so
            // these document-local ids only need to be internally consistent - they don't need
            // to be the real database ids of the existing client/project being targeted.
            Clients = [new ExportClient { Id = 1, Name = clientName, IsActive = true }],
            Projects = [new ExportProject { Id = 1, ClientId = 1, Name = projectName, IsActive = true }],
            TimeEntries = timeEntries,
            DaysOff = daysOff,
            NominalHoursSettings = nominalSettings,
        };
    }
}
