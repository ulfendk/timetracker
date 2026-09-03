using DbUp.Engine.Output;

namespace TrackMyTime.Web.Data;

/// <summary>Routes DbUp's migration log through the app's own <see cref="ILogger"/> instead of
/// the console, so migration output shows up in the app's HA log tab like everything else.</summary>
public sealed class MicrosoftLoggerUpgradeLog(ILogger logger) : IUpgradeLog
{
    public void LogTrace(string format, params object[] args) => logger.LogTrace(format, args);

    public void LogDebug(string format, params object[] args) => logger.LogDebug(format, args);

    public void LogInformation(string format, params object[] args) => logger.LogInformation(format, args);

    public void LogWarning(string format, params object[] args) => logger.LogWarning(format, args);

    public void LogError(string format, params object[] args) => logger.LogError(format, args);

    public void LogError(Exception ex, string format, params object[] args) => logger.LogError(ex, format, args);
}
