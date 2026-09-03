namespace TrackMyTime.Web.Services;

/// <summary>Monday-start week/month bounds used consistently by the Week/Month pages and the
/// MQTT publisher.</summary>
public static class DateRanges
{
    public static (DateOnly From, DateOnly To) WeekContaining(DateOnly date)
    {
        var offsetFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = date.AddDays(-offsetFromMonday);
        return (monday, monday.AddDays(6));
    }

    public static (DateOnly From, DateOnly To) MonthContaining(DateOnly date)
    {
        var first = new DateOnly(date.Year, date.Month, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }
}
