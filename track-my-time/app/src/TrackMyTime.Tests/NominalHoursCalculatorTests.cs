using TrackMyTime.Web.Models;
using TrackMyTime.Web.Services;

namespace TrackMyTime.Tests;

public class NominalHoursCalculatorTests
{
    private static readonly List<NominalHoursSetting> StandardWeek =
    [
        new() { EffectiveFrom = new DateOnly(2024, 1, 1), WeeklyHours = 37.5m },
    ];

    [Fact]
    public void FullWeekWithNoEntriesOrDaysOff_NominalIsWeeklyHours()
    {
        // Monday 2026-09-07 through Sunday 2026-09-13.
        var summary = NominalHoursCalculator.Summarize(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13),
            StandardWeek, [], []);

        Assert.Equal(37.5m, summary.NominalHours);
        Assert.Equal(0m, summary.ActualHours);
    }

    [Fact]
    public void WeekendEntries_CountAsActualButNotNominal()
    {
        var entries = new List<TimeEntry>
        {
            new() { Date = new DateOnly(2026, 9, 12), DurationMinutes = 120 }, // Saturday
        };

        var summary = NominalHoursCalculator.Summarize(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13),
            StandardWeek, [], entries);

        Assert.Equal(37.5m, summary.NominalHours);
        Assert.Equal(2m, summary.ActualWeekendHours);
        Assert.Equal(0m, summary.ActualWeekdayHours);
        Assert.Equal(2m, summary.ActualHours);
    }

    [Fact]
    public void DayOffOnAWeekday_SubtractsThatDaysShareFromNominal()
    {
        var daysOff = new List<DayOff> { new() { Date = new DateOnly(2026, 9, 8) } }; // Tuesday

        var summary = NominalHoursCalculator.Summarize(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13),
            StandardWeek, daysOff, []);

        // 37.5 / 5 = 7.5 per weekday; one weekday off => 30 nominal.
        Assert.Equal(30m, summary.NominalHours);
    }

    [Fact]
    public void DayOffOnAWeekend_HasNoEffectOnNominal()
    {
        var daysOff = new List<DayOff> { new() { Date = new DateOnly(2026, 9, 13) } }; // Sunday

        var summary = NominalHoursCalculator.Summarize(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13),
            StandardWeek, daysOff, []);

        Assert.Equal(37.5m, summary.NominalHours);
    }

    [Fact]
    public void EffectiveWeeklyHours_PicksLatestSettingOnOrBeforeDate()
    {
        var settings = new List<NominalHoursSetting>
        {
            new() { EffectiveFrom = new DateOnly(2024, 1, 1), WeeklyHours = 37.5m },
            new() { EffectiveFrom = new DateOnly(2026, 6, 1), WeeklyHours = 30m },
        };

        Assert.Equal(37.5m, NominalHoursCalculator.GetEffectiveWeeklyHours(settings, new DateOnly(2025, 12, 31)));
        Assert.Equal(30m, NominalHoursCalculator.GetEffectiveWeeklyHours(settings, new DateOnly(2026, 6, 1)));
        Assert.Equal(30m, NominalHoursCalculator.GetEffectiveWeeklyHours(settings, new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void NoSettingEffectiveYet_NominalIsZero()
    {
        var settings = new List<NominalHoursSetting>
        {
            new() { EffectiveFrom = new DateOnly(2027, 1, 1), WeeklyHours = 37.5m },
        };

        var summary = NominalHoursCalculator.Summarize(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13),
            settings, [], []);

        Assert.Equal(0m, summary.NominalHours);
    }
}
