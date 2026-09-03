using Dapper;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public sealed class ProjectRepository(SqliteConnectionFactory connectionFactory) : IProjectRepository
{
    public async Task<IReadOnlyList<ProjectWithClient>> GetAllAsync(bool includeInactive = false)
    {
        using var connection = connectionFactory.Open();
        var sql = $"""
            SELECT p.Id, p.ClientId, p.Name, p.IsActive, p.Color, c.Name AS ClientName
            FROM Project p
            JOIN Client c ON c.Id = p.ClientId
            {(includeInactive ? "" : "WHERE p.IsActive = 1 AND c.IsActive = 1")}
            ORDER BY c.Name, p.Name
            """;
        var projects = await connection.QueryAsync<ProjectWithClient>(sql);
        return projects.AsList();
    }

    public async Task<Project?> GetByIdAsync(int id)
    {
        using var connection = connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<Project>(
            "SELECT * FROM Project WHERE Id = @id", new { id });
    }

    public async Task<int> CreateAsync(Project project)
    {
        using var connection = connectionFactory.Open();
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO Project (ClientId, Name, IsActive, Color) VALUES (@ClientId, @Name, @IsActive, @Color);
            SELECT last_insert_rowid();
            """, project);
        return (int)id;
    }

    public async Task UpdateAsync(Project project)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Project SET ClientId = @ClientId, Name = @Name, IsActive = @IsActive, Color = @Color WHERE Id = @Id",
            project);
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE Project SET IsActive = @isActive WHERE Id = @id", new { id, isActive });
    }
}
