using System.Data;
using System.Runtime.CompilerServices;
using Dapper;
using TrackMyTime.Web.Models;

namespace TrackMyTime.Web.Data;

/// <summary>Stores <see cref="DayOffType"/> as its member name (TEXT), not Dapper's default
/// int-backed enum mapping - keeps the column human-readable when inspected by hand or read back
/// out through the export/import JSON.</summary>
public sealed class DayOffTypeHandler : SqlMapper.TypeHandler<DayOffType>
{
    public override void SetValue(IDbDataParameter parameter, DayOffType value) =>
        parameter.Value = value.ToString();

    public override DayOffType Parse(object value) =>
        Enum.Parse<DayOffType>((string)value);

    /// <summary>Runs automatically the moment this assembly loads (app startup, tests, anything
    /// referencing it) - no call site to remember/forget.</summary>
    [ModuleInitializer]
    public static void Register() => SqlMapper.AddTypeHandler(new DayOffTypeHandler());
}
