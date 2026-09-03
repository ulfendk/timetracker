using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public interface IClientRepository
{
    Task<IReadOnlyList<Client>> GetAllAsync(bool includeInactive = false);
    Task<Client?> GetByIdAsync(int id);
    Task<int> CreateAsync(Client client);
    Task UpdateAsync(Client client);
    Task SetActiveAsync(int id, bool isActive);
}
