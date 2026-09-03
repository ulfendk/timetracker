using TrackMyTime.Web.Models;
using TrackMyTime.Web.Services;

namespace TrackMyTime.Tests;

public class LegacyImportConverterTests
{
    private const string SampleJson = """
        {
          "dates": {
            "2026-01-26": {
              "intervals": [
                { "start": "08:15", "end": "14:00", "break": "30" },
                { "start": "15:00", "end": "16:45", "break": 0 }
              ],
              "isHoliday": false
            },
            "2026-01-27": { "intervals": [], "isHoliday": false },
            "2025-12-25": {
              "intervals": [],
              "isHoliday": true,
              "autoMarked": true,
              "holidayName": "Christmas Day"
            },
            "2026-07-07": {
              "intervals": [],
              "isHoliday": true,
              "holidayName": "Day Off"
            },
            "2026-08-07": {
              "intervals": [ { "start": "05:50", "end": "07:55", "break": "" } ],
              "isHoliday": false
            }
          },
          "selectedCountry": "DK",
          "holidays": { "DK-2025": [] },
          "weeklyExpectedHours": { "2026-07-06": 30 }
        }
        """;

    [Fact]
    public void GetDateRange_ReturnsMinAndMaxAcrossAllDateKeys()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var range = LegacyImportConverter.GetDateRange(export);

        Assert.NotNull(range);
        Assert.Equal(new DateOnly(2025, 12, 25), range!.Value.Min);
        Assert.Equal(new DateOnly(2026, 8, 7), range.Value.Max);
    }

    [Fact]
    public void Convert_ParsesIntervalsIntoTimeEntriesWithBreakSubtracted()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Acme", "Website");

        var entries = doc.TimeEntries.Where(t => t.Date == new DateOnly(2026, 1, 26)).ToList();
        Assert.Equal(2, entries.Count);
        var first = entries.Single(e => e.StartTime == new TimeOnly(8, 15));
        Assert.Equal(new TimeOnly(14, 0), first.EndTime);
        Assert.Equal(30, first.BreakMinutes);
        Assert.Equal(315, first.DurationMinutes); // 345 minutes - 30 break
        Assert.All(entries, e => Assert.Equal(1, e.ProjectId));
    }

    [Fact]
    public void Convert_HandlesEmptyStringBreakAsZero()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Acme", "Website");

        var entry = Assert.Single(doc.TimeEntries, e => e.Date == new DateOnly(2026, 8, 7));
        Assert.Equal(0, entry.BreakMinutes);
        Assert.Equal(125, entry.DurationMinutes); // 05:50-07:55 = 125 minutes, no break
    }

    [Fact]
    public void Convert_SkipsDatesWithEmptyIntervalsAndNoHoliday()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Acme", "Website");

        Assert.DoesNotContain(doc.TimeEntries, e => e.Date == new DateOnly(2026, 1, 27));
        Assert.DoesNotContain(doc.DaysOff, d => d.Date == new DateOnly(2026, 1, 27));
    }

    [Fact]
    public void Convert_MapsAutoMarkedHolidayToOfficialHolidayAndManualToVacation()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31), "Acme", "Website");

        var officialHoliday = Assert.Single(doc.DaysOff, d => d.Date == new DateOnly(2025, 12, 25));
        Assert.Equal(DayOffType.OfficialHoliday, officialHoliday.Type);
        var manualDayOff = Assert.Single(doc.DaysOff, d => d.Date == new DateOnly(2026, 7, 7));
        Assert.Equal(DayOffType.Vacation, manualDayOff.Type);
    }

    [Fact]
    public void Convert_ExcludesDatesOutsideTheChosenRange()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Acme", "Website");

        Assert.DoesNotContain(doc.DaysOff, d => d.Date == new DateOnly(2025, 12, 25));
        Assert.Empty(doc.NominalHoursSettings); // 2026-07-06 is outside the January-only range
    }

    [Fact]
    public void Convert_MapsWeeklyExpectedHoursToNominalHoursSettings()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Acme", "Website");

        var setting = Assert.Single(doc.NominalHoursSettings);
        Assert.Equal(new DateOnly(2026, 7, 6), setting.EffectiveFrom);
        Assert.Equal(30m, setting.WeeklyHours);
    }

    [Fact]
    public void Convert_UsesTheGivenClientAndProjectNames()
    {
        var export = LegacyImportConverter.Parse(SampleJson);

        var doc = LegacyImportConverter.Convert(export, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "My Client", "My Project");

        Assert.Equal("My Client", Assert.Single(doc.Clients).Name);
        Assert.Equal("My Project", Assert.Single(doc.Projects).Name);
    }
}
