using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public interface IDayOffRepository
{
    Task<IReadOnlyList<DayOff>> GetByDateRangeAsync(DateOnly from, DateOnly to);
    Task<DayOff?> GetByDateAsync(DateOnly date);
    Task<int> CreateAsync(DayOff dayOff);
    Task UpdateAsync(DayOff dayOff);
    Task DeleteAsync(int id);

    /// <summary>Count of days off of a given type within a date range (inclusive) - e.g. sick
    /// days year-to-date for the HA MQTT sensors.</summary>
    Task<int> CountByTypeAndDateRangeAsync(DayOffType type, DateOnly from, DateOnly to);
}
