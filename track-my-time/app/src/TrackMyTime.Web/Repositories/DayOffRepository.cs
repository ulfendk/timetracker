using Dapper;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public sealed class DayOffRepository(SqliteConnectionFactory connectionFactory) : IDayOffRepository
{
    public async Task<IReadOnlyList<DayOff>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        using var connection = connectionFactory.Open();
        var days = await connection.QueryAsync<DayOff>(
            "SELECT * FROM DayOff WHERE Date BETWEEN @from AND @to ORDER BY Date", new { from, to });
        return days.AsList();
    }

    public async Task<DayOff?> GetByDateAsync(DateOnly date)
    {
        using var connection = connectionFactory.Open();
        return await connection.QuerySingleOrDefaultAsync<DayOff>(
            "SELECT * FROM DayOff WHERE Date = @date", new { date });
    }

    public async Task<int> CreateAsync(DayOff dayOff)
    {
        using var connection = connectionFactory.Open();
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO DayOff (Date, Note, Type) VALUES (@Date, @Note, @Type);
            SELECT last_insert_rowid();
            """, dayOff);
        return (int)id;
    }

    public async Task UpdateAsync(DayOff dayOff)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE DayOff SET Date = @Date, Note = @Note, Type = @Type WHERE Id = @Id", dayOff);
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync("DELETE FROM DayOff WHERE Id = @id", new { id });
    }

    public async Task<int> CountByTypeAndDateRangeAsync(DayOffType type, DateOnly from, DateOnly to)
    {
        using var connection = connectionFactory.Open();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM DayOff WHERE Type = @type AND Date BETWEEN @from AND @to",
            new { type, from, to });
    }
}
