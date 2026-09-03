using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public interface INominalHoursRepository
{
    /// <summary>All settings ordered by EffectiveFrom ascending — the shape
    /// <see cref="Services.NominalHoursCalculator"/> expects.</summary>
    Task<IReadOnlyList<NominalHoursSetting>> GetAllAsync();

    Task<int> CreateAsync(NominalHoursSetting setting);
}
