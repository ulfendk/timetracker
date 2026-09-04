using TrackMyTime.Web.Models;
using TrackMyTime.Web.Repositories;

namespace TrackMyTime.Web.Services;

public sealed record TodaySummary(
    DateOnly Date,
    IReadOnlyList<TimeEntryWithNames> Entries,
    decimal TotalHours,
    bool IsDayOff);

/// <summary>Builds Today/Week/Month view models on top of the repositories. Shared by the UI
/// pages and <see cref="MqttPublisherService"/> so the two never disagree on what "actual" and
/// "nominal" mean.</summary>
public sealed class TimeSummaryService(
    ITimeEntryRepository timeEntryRepository,
    IDayOffRepository dayOffRepository,
    INominalHoursRepository nominalHoursRepository)
{
    public async Task<TodaySummary> GetTodayAsync(DateOnly date)
    {
        var entries = await timeEntryRepository.GetByDateAsync(date);
        var dayOff = await dayOffRepository.GetByDateAsync(date);
        var totalHours = entries.Sum(e => e.DurationMinutes) / 60m;
        return new TodaySummary(date, entries, totalHours, dayOff is not null);
    }

    public Task<PeriodSummary> GetWeekAsync(DateOnly anyDateInWeek, int weeksOffset = 0)
    {
        var (from, to) = DateRanges.WeekContaining(anyDateInWeek.AddDays(7 * weeksOffset));
        return SummarizeRangeAsync(from, to);
    }

    public Task<PeriodSummary> GetMonthAsync(DateOnly anyDateInMonth)
    {
        var (from, to) = DateRanges.MonthContaining(anyDateInMonth);
        return SummarizeRangeAsync(from, to);
    }

    private async Task<PeriodSummary> SummarizeRangeAsync(DateOnly from, DateOnly to)
    {
        var entries = await timeEntryRepository.GetByDateRangeAsync(from, to);
        var daysOff = await dayOffRepository.GetByDateRangeAsync(from, to);
        var settings = await nominalHoursRepository.GetAllAsync();

        var plainEntries = entries
            .Select(e => new TimeEntry { Id = e.Id, Date = e.Date, ProjectId = e.ProjectId, DurationMinutes = e.DurationMinutes, Note = e.Note })
            .ToList();

        return NominalHoursCalculator.Summarize(from, to, settings, daysOff, plainEntries);
    }
}
