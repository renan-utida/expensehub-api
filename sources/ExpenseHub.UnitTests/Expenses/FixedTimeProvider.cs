using System;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Hand-written clock that always returns the same instant.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="FixedTimeProvider"/> class.</summary>
    /// <param name="now">The instant the clock returns.</param>
    public FixedTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }
}
