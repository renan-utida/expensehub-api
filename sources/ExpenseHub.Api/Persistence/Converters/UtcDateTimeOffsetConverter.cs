using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExpenseHub.Api.Persistence.Converters;

/// <summary>
/// Stores a <see cref="DateTimeOffset"/> as UTC ticks. The logic lives in <see cref="UtcTicks"/>.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, long>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UtcDateTimeOffsetConverter"/> class.
    /// </summary>
    public UtcDateTimeOffsetConverter()
        : base(instant => UtcTicks.From(instant), ticks => UtcTicks.ToDateTimeOffset(ticks))
    {
    }
}
