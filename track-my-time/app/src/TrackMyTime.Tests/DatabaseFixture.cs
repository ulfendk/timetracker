using Microsoft.Extensions.Logging.Abstractions;
using TrackMyTime.Web.Data;

namespace TrackMyTime.Tests;

/// <summary>Points TMT_DATA_DIR at a fresh temp folder and runs the real DbUp migrations
/// against a real SQLite file before any repository test runs — this is what actually catches
/// issues like Dapper/DateOnly round-tripping that a mocked repository never would.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "tmt-tests-" + Guid.NewGuid());

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("TMT_DATA_DIR", _tempDir);
        await DatabaseInitializer.InitializeAsync(NullLogger.Instance);
        ConnectionFactory = new SqliteConnectionFactory();
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort - a stray open handle on some platforms shouldn't fail the test run.
        }
        return Task.CompletedTask;
    }
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
