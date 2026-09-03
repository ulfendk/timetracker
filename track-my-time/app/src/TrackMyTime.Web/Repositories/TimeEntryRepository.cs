using Dapper;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public sealed class TimeEntryRepository(SqliteConnectionFactory connectionFactory) : ITimeEntryRepository
{
    private const string SelectWithNames = """
        SELECT t.Id, t.Date, t.ProjectId, t.DurationMinutes, t.Note,
               t.StartTime, t.EndTime, t.BreakMinutes,
               p.Name AS ProjectName, p.Color, c.Name AS ClientName
        FROM TimeEntry t
        JOIN Project p ON p.Id = t.ProjectId
        JOIN Client c ON c.Id = p.ClientId
        """;

    public async Task<IReadOnlyList<TimeEntryWithNames>> GetByDateAsync(DateOnly date)
    {
        using var connection = connectionFactory.Open();
        var entries = await connection.QueryAsync<TimeEntryWithNames>(
            $"{SelectWithNames} WHERE t.Date = @date ORDER BY t.Id", new { date });
        return entries.AsList();
    }

    public async Task<IReadOnlyList<TimeEntryWithNames>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        using var connection = connectionFactory.Open();
        var entries = await connection.QueryAsync<TimeEntryWithNames>(
            $"{SelectWithNames} WHERE t.Date BETWEEN @from AND @to ORDER BY t.Date, t.Id",
            new { from, to });
        return entries.AsList();
    }

    public async Task<int> CreateAsync(TimeEntry entry)
    {
        using var connection = connectionFactory.Open();
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO TimeEntry (Date, ProjectId, DurationMinutes, Note, StartTime, EndTime, BreakMinutes)
            VALUES (@Date, @ProjectId, @DurationMinutes, @Note, @StartTime, @EndTime, @BreakMinutes);
            SELECT last_insert_rowid();
            """, entry);
        return (int)id;
    }

    public async Task UpdateAsync(TimeEntry entry)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            """
            UPDATE TimeEntry
            SET Date = @Date, ProjectId = @ProjectId, DurationMinutes = @DurationMinutes, Note = @Note,
                StartTime = @StartTime, EndTime = @EndTime, BreakMinutes = @BreakMinutes
            WHERE Id = @Id
            """, entry);
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync("DELETE FROM TimeEntry WHERE Id = @id", new { id });
    }
}
