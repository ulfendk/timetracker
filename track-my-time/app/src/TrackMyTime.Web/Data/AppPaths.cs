namespace TrackMyTime.Web.Data;

/// <summary>Resolves where persistent data lives. Under the HA app framework, "/data" is the
/// one path Supervisor keeps across image/version upgrades — everything durable must live under
/// it. Outside of HA (plain `dotnet run` for local development), we fall back to a "./data"
/// folder next to the working directory so there's no need for HA to develop against.</summary>
public static class AppPaths
{
    /// <summary>Override via the TMT_DATA_DIR environment variable if you need a custom location
    /// (e.g. for tests). Otherwise: "/data" when it exists (HA app container), else "./data".</summary>
    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string DatabasePath => Path.Combine(DataDirectory, "trackmytime.db");

    public static string BackupsDirectory => Path.Combine(DataDirectory, "backups");

    private static string ResolveDataDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("TMT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            Directory.CreateDirectory(overridePath);
            return overridePath;
        }

        var dataDir = Directory.Exists("/data") ? "/data" : Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        return dataDir;
    }
}
