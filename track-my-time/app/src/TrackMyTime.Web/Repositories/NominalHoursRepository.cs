using Dapper;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Repositories;

public sealed class NominalHoursRepository(SqliteConnectionFactory connectionFactory) : INominalHoursRepository
{
    public async Task<IReadOnlyList<NominalHoursSetting>> GetAllAsync()
    {
        using var connection = connectionFactory.Open();
        var settings = await connection.QueryAsync<NominalHoursSetting>(
            "SELECT * FROM NominalHoursSetting ORDER BY EffectiveFrom");
        return settings.AsList();
    }

    public async Task<int> CreateAsync(NominalHoursSetting setting)
    {
        using var connection = connectionFactory.Open();
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO NominalHoursSetting (EffectiveFrom, WeeklyHours) VALUES (@EffectiveFrom, @WeeklyHours);
            SELECT last_insert_rowid();
            """, setting);
        return (int)id;
    }

    public async Task UpdateAsync(NominalHoursSetting setting)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync(
            "UPDATE NominalHoursSetting SET EffectiveFrom = @EffectiveFrom, WeeklyHours = @WeeklyHours WHERE Id = @Id",
            setting);
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = connectionFactory.Open();
        await connection.ExecuteAsync("DELETE FROM NominalHoursSetting WHERE Id = @id", new { id });
    }
}
