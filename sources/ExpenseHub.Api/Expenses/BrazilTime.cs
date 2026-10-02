using System;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Today's date in Brazil (Brasilia time), the reference of the "not in the future" rule of the expense date.
/// </summary>
public static class BrazilTime
{
    private static readonly TimeZoneInfo _zone = FindZone();

    /// <summary>
    /// Gets the date in Brasilia time of an instant.
    /// </summary>
    /// <param name="utcNow">The instant.</param>
    /// <returns>The calendar date in Brasilia time.</returns>
    public static DateOnly Today(DateTimeOffset utcNow)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, _zone).DateTime);
    }

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
