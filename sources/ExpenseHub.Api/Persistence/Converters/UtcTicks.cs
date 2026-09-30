using System;

namespace ExpenseHub.Api.Persistence.Converters;

/// <summary>
/// Converts instants between <see cref="DateTimeOffset"/> and UTC ticks.
/// SQLite stores DateTimeOffset as text, which cannot be ordered or compared, so instants are stored as ticks.
/// </summary>
public static class UtcTicks
{
    /// <summary>
    /// Converts an instant to UTC ticks, whatever its offset.
    /// </summary>
    /// <param name="instant">The instant to convert.</param>
    /// <returns>The UTC ticks of the instant.</returns>
    public static long From(DateTimeOffset instant) => instant.UtcTicks;

    /// <summary>
    /// Converts UTC ticks back to an instant with offset zero.
    /// </summary>
    /// <param name="ticks">The UTC ticks.</param>
    /// <returns>The instant in UTC.</returns>
    public static DateTimeOffset ToDateTimeOffset(long ticks) => new DateTimeOffset(ticks, TimeSpan.Zero);
}
