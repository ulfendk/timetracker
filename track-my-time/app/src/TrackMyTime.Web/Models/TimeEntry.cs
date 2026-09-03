namespace TrackMyTime.Web.Models;

/// <summary>A logged block of time against a project on a given day. Weekend entries are
/// allowed and counted as "actual" hours; they simply never contribute to the nominal target.
/// Weekday/weekend is derived from <see cref="Date"/> at query time, not stored.</summary>
public sealed class TimeEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int ProjectId { get; set; }
    public int DurationMinutes { get; set; }
    public string? Note { get; set; }

    /// <summary>Entry/display metadata only - DurationMinutes stays the authoritative stored
    /// value, computed from these at save time. Null on entries logged before this existed, or
    /// created via plain duration entry (e.g. import); those display as duration-only.</summary>
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? BreakMinutes { get; set; }
}

/// <summary>A <see cref="TimeEntry"/> joined with its project/client names, for list views.</summary>
public sealed class TimeEntryWithNames
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int ProjectId { get; set; }
    public int DurationMinutes { get; set; }
    public string? Note { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? BreakMinutes { get; set; }
    public required string ProjectName { get; set; }
    public required string ClientName { get; set; }
    public string? Color { get; set; }
}
