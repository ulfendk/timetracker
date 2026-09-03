using Microsoft.Data.Sqlite;

namespace TrackMyTime.Web.Data;

/// <summary>Hands out open, WAL-mode SQLite connections against the app database. Short-lived,
/// one per unit of work — SQLite/Dapper don't need pooling the way a server DB would.</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // WAL mode lets reads and writes proceed concurrently and is required for the
        // VACUUM INTO backup strategy to snapshot a live database cleanly.
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
