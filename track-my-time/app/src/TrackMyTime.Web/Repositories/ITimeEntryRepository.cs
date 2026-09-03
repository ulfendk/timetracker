using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public interface ITimeEntryRepository
{
    Task<IReadOnlyList<TimeEntryWithNames>> GetByDateAsync(DateOnly date);
    Task<IReadOnlyList<TimeEntryWithNames>> GetByDateRangeAsync(DateOnly from, DateOnly to);
    Task<int> CreateAsync(TimeEntry entry);
    Task UpdateAsync(TimeEntry entry);
    Task DeleteAsync(int id);
}
