using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Services;

/// <summary>Pure logic for backlog #22 (day-off ranges + collapsing) - no I/O, so it's cheap to
/// unit test directly against hand-built lists of <see cref="DayOff"/> rows.</summary>
public static class DayOffGrouping
{
    /// <summary>Dates in [from, to] eligible for a new day off. Weekends are never marked as off,
    /// and neither is any date that already has a day off recorded - which also covers "official
    /// holidays count as weekend days" (a holiday inside the range already has its own row, so
    /// it's skipped here rather than getting a second, conflicting one). A 2-week vacation
    /// spanning a weekend therefore only yields the working days.</summary>
    public static IEnumerable<DateOnly> ExpandRange(DateOnly from, DateOnly to, IReadOnlyCollection<DateOnly> existingDates)
    {
        var existing = existingDates as HashSet<DateOnly> ?? existingDates.ToHashSet();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (NominalHoursCalculator.IsWeekend(date) || existing.Contains(date))
            {
                continue;
            }
            yield return date;
        }
    }

    /// <summary>Collapses consecutive same-type days off into runs, even when the run is
    /// interrupted by a weekend or an official holiday - those gap days don't break the run since
    /// they'd never get their own row of this type anyway. <paramref name="days"/> need not be
    /// pre-sorted.</summary>
    public static List<DayOffGroup> BuildGroups(IReadOnlyList<DayOff> days)
    {
        var officialHolidayDates = days.Where(d => d.Type == DayOffType.OfficialHoliday).Select(d => d.Date).ToHashSet();
        var groups = new List<DayOffGroup>();

        // At most one "open" (still-extendable) group per Type, tracked in this list rather than
        // a Dictionary keyed by DayOffType? - Type is nullable ("unspecified", see DayOff.cs) and
        // Dictionary rejects a null key even for a nullable value-type TKey. Tracking per type
        // (not just "the most recently seen group") lets a same-type run jump over an interleaved
        // different-type group - e.g. a public holiday recorded in the middle of a vacation run -
        // as long as everything in between is bridgeable. Linear scan is fine: there are at most
        // a handful of DayOffType values.
        var openGroups = new List<DayOffGroup>();

        foreach (var day in days.OrderBy(d => d.Date))
        {
            var open = openGroups.Find(g => g.Type == day.Type);
            if (open is not null && IsBridgeableGap(open.Days[^1].Date, day.Date, officialHolidayDates))
            {
                open.Days.Add(day);
            }
            else
            {
                if (open is not null)
                {
                    openGroups.Remove(open);
                }
                var group = new DayOffGroup { Type = day.Type };
                group.Days.Add(day);
                groups.Add(group);
                openGroups.Add(group);
            }
        }

        return groups;
    }

    private static bool IsBridgeableGap(DateOnly from, DateOnly to, HashSet<DateOnly> officialHolidayDates)
    {
        for (var day = from.AddDays(1); day < to; day = day.AddDays(1))
        {
            if (!NominalHoursCalculator.IsWeekend(day) && !officialHolidayDates.Contains(day))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>One or more consecutive <see cref="DayOff"/> rows of the same type, displayed as a
/// single row with a total day count.</summary>
public sealed class DayOffGroup
{
    public DayOffType? Type { get; set; }
    public List<DayOff> Days { get; } = [];
    public DateOnly Start => Days[0].Date;
    public DateOnly End => Days[^1].Date;
    public int Count => Days.Count;
    public string? Note => Days.Select(d => d.Note).Distinct().Count() == 1 ? Days[0].Note : null;
}
