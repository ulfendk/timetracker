using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dapper;

namespace TrackMyTime.Web.Data;

/// <summary>Dapper has no built-in DateOnly support for Microsoft.Data.Sqlite (unlike SqlClient,
/// it doesn't advertise a DbType for it), so parameterized DateOnly values throw
/// NotSupportedException without this. Stores as ISO "yyyy-MM-dd" TEXT, which also sorts and
/// range-compares correctly as plain text in SQLite's dynamic typing.</summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override void SetValue(IDbDataParameter parameter, DateOnly value) =>
        parameter.Value = value.ToString(Format, CultureInfo.InvariantCulture);

    public override DateOnly Parse(object value) =>
        DateOnly.ParseExact((string)value, Format, CultureInfo.InvariantCulture);

    /// <summary>Runs automatically the moment this assembly loads (app startup, tests, anything
    /// referencing it) - no call site to remember/forget.</summary>
    [ModuleInitializer]
    public static void Register() => SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
}
