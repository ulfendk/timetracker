using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackMyTime.Web.Models.Legacy;

/// <summary>Shape of a third-party time tracker's export - NOT Track My Time's own format (see
/// Models/Export/ExportDocument.cs for that). Used only as a one-time on-ramp: converted via
/// LegacyImportConverter into an ExportDocument, then applied through the same preview/conflict
/// flow as a normal import. "holidays" (a public-holiday reference calendar) and
/// "selectedCountry" are intentionally not modeled - they aren't the user's actual logged data.</summary>
public sealed class LegacyExport
{
    public Dictionary<string, LegacyDateEntry> Dates { get; set; } = [];
    public Dictionary<string, decimal> WeeklyExpectedHours { get; set; } = [];
}

public sealed class LegacyDateEntry
{
    public List<LegacyInterval> Intervals { get; set; } = [];
    public bool IsHoliday { get; set; }
    public bool AutoMarked { get; set; }
    public string? HolidayName { get; set; }
}

public sealed class LegacyInterval
{
    public required string Start { get; set; }
    public required string End { get; set; }

    /// <summary>The source data is inconsistent about this field's JSON type - seen as a number
    /// (0), a numeric string ("30"), and an empty string (""). This converter normalizes all
    /// three to a plain int (empty/unparseable -> 0) rather than throwing.</summary>
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Break { get; set; }
}

public sealed class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed,
            _ => 0,
        };

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
