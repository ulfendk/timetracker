using TrackMyTime.Web.Models;
using TrackMyTime.Web.Services;

namespace TrackMyTime.Tests;

public class DayOffGroupingTests
{
    [Fact]
    public void ExpandRange_SkipsWeekends()
    {
        // Friday 2026-09-11 through Monday 2026-09-14 (spans one weekend).
        var dates = DayOffGrouping.ExpandRange(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14), []).ToList();

        Assert.Equal([new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14)], dates);
    }

    [Fact]
    public void ExpandRange_SkipsDatesThatAlreadyHaveADayOff()
    {
        // A Wednesday inside the range already has a day off recorded (e.g. an official holiday).
        var existing = new HashSet<DateOnly> { new(2026, 9, 9) };

        var dates = DayOffGrouping.ExpandRange(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 11), existing).ToList();

        Assert.DoesNotContain(new DateOnly(2026, 9, 9), dates);
        Assert.Equal(4, dates.Count); // Mon, Tue, Thu, Fri.
    }

    [Fact]
    public void BuildGroups_CollapsesConsecutiveSameTypeDaysIntoOneGroup()
    {
        var days = new List<DayOff>
        {
            new() { Id = 1, Date = new DateOnly(2026, 9, 7), Type = DayOffType.Vacation },
            new() { Id = 2, Date = new DateOnly(2026, 9, 8), Type = DayOffType.Vacation },
            new() { Id = 3, Date = new DateOnly(2026, 9, 9), Type = DayOffType.Vacation },
        };

        var groups = DayOffGrouping.BuildGroups(days);

        var group = Assert.Single(groups);
        Assert.Equal(3, group.Count);
        Assert.Equal(new DateOnly(2026, 9, 7), group.Start);
        Assert.Equal(new DateOnly(2026, 9, 9), group.End);
    }

    [Fact]
    public void BuildGroups_BridgesAWeekendWithinTheSameRun()
    {
        // Friday + the following Monday, same type - the weekend in between shouldn't split them.
        var days = new List<DayOff>
        {
            new() { Id = 1, Date = new DateOnly(2026, 9, 11), Type = DayOffType.Vacation }, // Friday
            new() { Id = 2, Date = new DateOnly(2026, 9, 14), Type = DayOffType.Vacation }, // Monday
        };

        var groups = DayOffGrouping.BuildGroups(days);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void BuildGroups_BridgesAnOfficialHolidayWithinTheSameRun()
    {
        // A full working week off (Mon-Fri), with a public holiday recorded on the Wednesday -
        // the vacation run should read as one continuous Mon-Fri group, not split around it.
        var days = new List<DayOff>
        {
            new() { Id = 1, Date = new DateOnly(2026, 9, 7), Type = DayOffType.Vacation }, // Monday
            new() { Id = 2, Date = new DateOnly(2026, 9, 8), Type = DayOffType.Vacation }, // Tuesday
            new() { Id = 3, Date = new DateOnly(2026, 9, 9), Type = DayOffType.OfficialHoliday }, // Wednesday
            new() { Id = 4, Date = new DateOnly(2026, 9, 10), Type = DayOffType.Vacation }, // Thursday
            new() { Id = 5, Date = new DateOnly(2026, 9, 11), Type = DayOffType.Vacation }, // Friday
        };

        var groups = DayOffGrouping.BuildGroups(days);

        Assert.Equal(2, groups.Count);
        var vacation = groups.Single(g => g.Type == DayOffType.Vacation);
        Assert.Equal(4, vacation.Count);
        Assert.Equal(new DateOnly(2026, 9, 7), vacation.Start);
        Assert.Equal(new DateOnly(2026, 9, 11), vacation.End);
        var holiday = groups.Single(g => g.Type == DayOffType.OfficialHoliday);
        Assert.Equal(1, holiday.Count);
    }

    [Fact]
    public void BuildGroups_DifferentTypesDoNotMerge()
    {
        var days = new List<DayOff>
        {
            new() { Id = 1, Date = new DateOnly(2026, 9, 7), Type = DayOffType.Sickness },
            new() { Id = 2, Date = new DateOnly(2026, 9, 8), Type = DayOffType.Vacation },
        };

        var groups = DayOffGrouping.BuildGroups(days);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void BuildGroups_HandlesLegacyRowsWithNoType()
    {
        // Type is nullable ("unspecified" for rows that predate it, see DayOff.cs) - grouping
        // must not throw when it's null, and consecutive null-type days should still merge.
        var days = new List<DayOff>
        {
            new() { Id = 1, Date = new DateOnly(2026, 9, 7), Type = null },
            new() { Id = 2, Date = new DateOnly(2026, 9, 8), Type = null },
        };

        var groups = DayOffGrouping.BuildGroups(days);

        var group = Assert.Single(groups);
        Assert.Null(group.Type);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void BuildGroups_NonAdjacentSameTypeDaysDoNotMerge()
    {
        // A gap of an actual working day between two Vacation entries should not bridge them.
        var days = new List<DayOff>
        {
            new() { Id = 1, Date = new DateOnly(2026, 9, 7), Type = DayOffType.Vacation }, // Monday
            new() { Id = 2, Date = new DateOnly(2026, 9, 9), Type = DayOffType.Vacation }, // Wednesday
        };

        var groups = DayOffGrouping.BuildGroups(days);

        Assert.Equal(2, groups.Count);
    }
}
