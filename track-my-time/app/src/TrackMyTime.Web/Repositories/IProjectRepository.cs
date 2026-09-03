using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectWithClient>> GetAllAsync(bool includeInactive = false);
    Task<Project?> GetByIdAsync(int id);
    Task<int> CreateAsync(Project project);
    Task UpdateAsync(Project project);
    Task SetActiveAsync(int id, bool isActive);
}
