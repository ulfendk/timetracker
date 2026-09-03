namespace TrackMyTime.Web.Models.Export;

/// <summary>Track My Time's own JSON export/import format - a straightforward dump/reload of
/// every table. Ids here are document-local (self-consistent within the file, e.g.
/// ExportProject.ClientId points at an ExportClient.Id in the same document) - they are NOT the
/// source database's real autoincrement ids, so a document can be freely merged into any target
/// database. SchemaVersion lets a future shape change be detected on import.</summary>
public sealed class ExportDocument
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset ExportedAtUtc { get; set; }
    public List<ExportClient> Clients { get; set; } = [];
    public List<ExportProject> Projects { get; set; } = [];
    public List<ExportTimeEntry> TimeEntries { get; set; } = [];
    public List<ExportDayOff> DaysOff { get; set; } = [];
    public List<ExportNominalHoursSetting> NominalHoursSettings { get; set; } = [];
}

public sealed class ExportClient
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ExportProject
{
    public int Id { get; set; }

    /// <summary>References an <see cref="ExportClient.Id"/> within this same document.</summary>
    public int ClientId { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
}

public sealed class ExportTimeEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>References an <see cref="ExportProject.Id"/> within this same document.</summary>
    public int ProjectId { get; set; }
    public int DurationMinutes { get; set; }
    public string? Note { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? BreakMinutes { get; set; }
}

public sealed class ExportDayOff
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public DayOffType? Type { get; set; }
}

public sealed class ExportNominalHoursSetting
{
    public int Id { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal WeeklyHours { get; set; }
}
