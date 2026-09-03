using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dapper;

namespace TrackMyTime.Web.Data;

/// <summary>Dapper has no built-in TimeOnly support for Microsoft.Data.Sqlite, same gap as
/// DateOnly (see <see cref="DateOnlyTypeHandler"/>). Stores as "HH:mm" TEXT - minute precision is
/// all the UI collects, and it still sorts/range-compares correctly as plain text.</summary>
public sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    private const string Format = "HH:mm";

    public override void SetValue(IDbDataParameter parameter, TimeOnly value) =>
        parameter.Value = value.ToString(Format, CultureInfo.InvariantCulture);

    public override TimeOnly Parse(object value) =>
        TimeOnly.ParseExact((string)value, Format, CultureInfo.InvariantCulture);

    /// <summary>Runs automatically the moment this assembly loads (app startup, tests, anything
    /// referencing it) - no call site to remember/forget.</summary>
    [ModuleInitializer]
    public static void Register() => SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
}
