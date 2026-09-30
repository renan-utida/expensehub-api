using System;

namespace ExpenseHub.Api.Persistence.Converters;

/// <summary>
/// Converts money between <see cref="decimal"/> and integer cents.
/// SQLite stores decimal as text, which cannot be ordered or compared, so amounts are stored as cents.
/// </summary>
public static class MoneyConversion
{
    private const decimal CentsPerUnit = 100m;

    /// <summary>
    /// Converts an amount to integer cents, rounding half a cent away from zero.
    /// </summary>
    /// <param name="amount">The amount in currency units.</param>
    /// <returns>The amount in cents.</returns>
    /// <exception cref="OverflowException">The amount does not fit in a <see cref="long"/> of cents.</exception>
    public static long ToCents(decimal amount) =>
        (long)Math.Round(amount * CentsPerUnit, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts integer cents to an amount in currency units.
    /// </summary>
    /// <param name="cents">The amount in cents.</param>
    /// <returns>The amount in currency units.</returns>
    public static decimal FromCents(long cents) => cents / CentsPerUnit;
}
