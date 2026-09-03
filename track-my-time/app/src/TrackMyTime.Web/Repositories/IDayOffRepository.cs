using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public interface IDayOffRepository
{
    Task<IReadOnlyList<DayOff>> GetByDateRangeAsync(DateOnly from, DateOnly to);
    Task<DayOff?> GetByDateAsync(DateOnly date);
    Task<int> CreateAsync(DayOff dayOff);
    Task DeleteAsync(int id);
}
