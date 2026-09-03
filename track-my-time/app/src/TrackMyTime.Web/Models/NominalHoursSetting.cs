namespace TrackMyTime.Web.Models;

/// <summary>Your contracted weekly hours (e.g. 37.5), effective from a given date. Stored as a
/// dated setting rather than a single global number so a future contract change doesn't rewrite
/// history: any given day picks whichever setting is effective on or before that day.</summary>
public sealed class NominalHoursSetting
{
    public int Id { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal WeeklyHours { get; set; }
}
