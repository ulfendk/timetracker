using Dapper;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public sealed class ClientRepository(SqliteConnectionFactory connectionFactory) : IClientRepository
{
    public async Task<IReadOnlyList<Client>> GetAllAsync(bool includeInactive = false)
    {
        using var connection = connectionFactory.Open();
        var sql = includeInactive
            ? "SELECT * FROM Client ORDER BY Name"
            : "SELECT * FROM Client WHERE IsActive = 1 ORDER BY Name";
        var clients = await connection.QueryAsync<Client>(sql);
        return clients.AsList();
    }

    public async Task<Client?> GetByIdAsync(int id)
    {
        using var connection = connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<Client>(
            "SELECT * FROM Client WHERE Id = @id", new { id });
    }

    public async Task<int> CreateAsync(Client client)
    {
        using var connection = connectionFactory.Open();
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO Client (Name, IsActive) VALUES (@Name, @IsActive);
            SELECT last_insert_rowid();
            """, client);
        return (int)id;
    }

    public async Task UpdateAsync(Client client)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Client SET Name = @Name, IsActive = @IsActive WHERE Id = @Id", client);
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Client SET IsActive = @isActive WHERE Id = @id", new { id, isActive });
    }
}
