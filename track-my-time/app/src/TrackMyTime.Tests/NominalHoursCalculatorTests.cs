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

    [Fact]
    public void SummarizeByWeekday_SplitsHoursIntoCorrectWeekdayBuckets()
    {
        // Monday 2026-09-07 through Sunday 2026-09-13.
        var entries = new List<TimeEntry>
        {
            new() { Date = new DateOnly(2026, 9, 7), DurationMinutes = 8 * 60 },  // Monday
            new() { Date = new DateOnly(2026, 9, 9), DurationMinutes = 6 * 60 },  // Wednesday
        };

        var byWeekday = NominalHoursCalculator.SummarizeByWeekday(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13), StandardWeek, [], entries);

        Assert.Equal(7, byWeekday.Count);
        Assert.Equal(DayOfWeek.Monday, byWeekday[0].DayOfWeek);
        Assert.Equal(8m, byWeekday[0].ActualHours);
        Assert.Equal(DayOfWeek.Wednesday, byWeekday[2].DayOfWeek);
        Assert.Equal(6m, byWeekday[2].ActualHours);
        Assert.Equal(DayOfWeek.Sunday, byWeekday[6].DayOfWeek);
        Assert.Equal(0m, byWeekday[6].ActualHours);
    }

    [Fact]
    public void SummarizeByWeekday_ExcludesWeekendsAndDaysOffFromNominal()
    {
        var daysOff = new List<DayOff> { new() { Date = new DateOnly(2026, 9, 8) } }; // Tuesday

        var byWeekday = NominalHoursCalculator.SummarizeByWeekday(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13), StandardWeek, daysOff, []);

        Assert.Equal(7.5m, byWeekday.Single(w => w.DayOfWeek == DayOfWeek.Monday).NominalHours);
        Assert.Equal(0m, byWeekday.Single(w => w.DayOfWeek == DayOfWeek.Tuesday).NominalHours);
        Assert.Equal(0m, byWeekday.Single(w => w.DayOfWeek == DayOfWeek.Saturday).NominalHours);
        Assert.Equal(0m, byWeekday.Single(w => w.DayOfWeek == DayOfWeek.Sunday).NominalHours);
    }

    [Fact]
    public void Summarize_NominalDaysCountsEligibleWeekdaysOnly()
    {
        // Monday 2026-09-07 through Sunday 2026-09-13, with one weekday day off (Wednesday).
        var daysOff = new List<DayOff> { new() { Date = new DateOnly(2026, 9, 9) } };

        var summary = NominalHoursCalculator.Summarize(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13),
            StandardWeek, daysOff, []);

        // 5 weekdays minus the 1 day off = 4 nominal days.
        Assert.Equal(4, summary.NominalDays);
    }
}
