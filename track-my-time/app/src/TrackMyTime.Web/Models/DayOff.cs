namespace TrackMyTime.Web.Models;

/// <summary>Marks a whole day as off. If that day is a weekday, it subtracts that day's share of
/// the nominal weekly hours (WeeklyHours / 5) from the week's and month's nominal target. A
/// weekend day marked off has no effect on nominal hours (weekends never contribute to it).</summary>
public sealed class DayOff
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }

    /// <summary>Null means "unspecified" - existing rows predate this field and are never
    /// backfilled with a guessed category.</summary>
    public DayOffType? Type { get; set; }
}
