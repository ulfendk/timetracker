using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Services;

/// <summary>Actual vs. nominal hours for a date range (inclusive). Nominal only ever accrues on
/// weekdays; actual hours are broken into weekday/weekend so weekend work reads as overtime on
/// top of the target rather than blending into it.</summary>
public sealed record PeriodSummary(
    DateOnly From,
    DateOnly To,
    decimal NominalHours,
    decimal ActualWeekdayHours,
    decimal ActualWeekendHours,
    IReadOnlyList<DateOnly> DaysOff,
    int NominalDays = 0)
{
    public decimal ActualHours => ActualWeekdayHours + ActualWeekendHours;

    /// <summary>Positive = ahead of target, negative = behind.</summary>
    public decimal DeltaHours => ActualHours - NominalHours;
}

/// <summary>Pure calculation logic for nominal vs. actual hours — no I/O, so it's cheap to unit
/// test directly against hand-built lists of settings/entries.</summary>
public static class NominalHoursCalculator
{
    /// <summary>The weekly nominal hours in effect on <paramref name="date"/>: the latest
    /// setting whose EffectiveFrom is on or before that date. <paramref name="settings"/> must
    /// be sorted ascending by EffectiveFrom (as returned by the repository).</summary>
    public static decimal GetEffectiveWeeklyHours(IReadOnlyList<NominalHoursSetting> settings, DateOnly date)
    {
        NominalHoursSetting? current = null;
        foreach (var setting in settings)
        {
            if (setting.EffectiveFrom > date)
            {
                break;
            }
            current = setting;
        }
        return current?.WeeklyHours ?? 0m;
    }

    public static PeriodSummary Summarize(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<NominalHoursSetting> nominalSettings,
        IReadOnlyList<DayOff> daysOff,
        IReadOnlyList<TimeEntry> entries)
    {
        var daysOffInRange = daysOff.Where(d => d.Date >= from && d.Date <= to).Select(d => d.Date).ToList();
        var daysOffSet = daysOffInRange.ToHashSet();

        var nominal = 0m;
        var nominalDays = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (IsWeekend(day) || daysOffSet.Contains(day))
            {
                continue;
            }
            nominal += GetEffectiveWeeklyHours(nominalSettings, day) / 5m;
            nominalDays++;
        }

        var weekdayMinutes = 0;
        var weekendMinutes = 0;
        foreach (var entry in entries)
        {
            if (entry.Date < from || entry.Date > to)
            {
                continue;
            }
            if (IsWeekend(entry.Date))
            {
                weekendMinutes += entry.DurationMinutes;
            }
            else
            {
                weekdayMinutes += entry.DurationMinutes;
            }
        }

        return new PeriodSummary(from, to, nominal, weekdayMinutes / 60m, weekendMinutes / 60m, daysOffInRange, nominalDays);
    }

    public static bool IsWeekend(DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Actual vs. nominal hours broken down per weekday (Monday-first) across the given
    /// range, rather than collapsed into one total - e.g. to chart "do I consistently work more
    /// on Mondays than Fridays?". Uses the exact same weekend/day-off/effective-hours rules as
    /// <see cref="Summarize"/>.</summary>
    public static IReadOnlyList<WeekdaySummary> SummarizeByWeekday(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<NominalHoursSetting> nominalSettings,
        IReadOnlyList<DayOff> daysOff,
        IReadOnlyList<TimeEntry> entries)
    {
        var daysOffSet = daysOff.Where(d => d.Date >= from && d.Date <= to).Select(d => d.Date).ToHashSet();

        var nominalByWeekday = new decimal[7];
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (IsWeekend(day) || daysOffSet.Contains(day))
            {
                continue;
            }
            nominalByWeekday[MondayFirstIndex(day.DayOfWeek)] += GetEffectiveWeeklyHours(nominalSettings, day) / 5m;
        }

        var actualMinutesByWeekday = new int[7];
        foreach (var entry in entries)
        {
            if (entry.Date < from || entry.Date > to)
            {
                continue;
            }
            actualMinutesByWeekday[MondayFirstIndex(entry.Date.DayOfWeek)] += entry.DurationMinutes;
        }

        return Enum.GetValues<DayOfWeek>()
            .OrderBy(MondayFirstIndex)
            .Select(dow => new WeekdaySummary(dow, actualMinutesByWeekday[MondayFirstIndex(dow)] / 60m, nominalByWeekday[MondayFirstIndex(dow)]))
            .ToList();
    }

    private static int MondayFirstIndex(DayOfWeek dayOfWeek) => ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
}

/// <summary>Actual vs. nominal hours for a single weekday, summed across a date range.</summary>
public sealed record WeekdaySummary(DayOfWeek DayOfWeek, decimal ActualHours, decimal NominalHours);
