using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExpenseHub.Api.Persistence.Converters;

/// <summary>
/// Stores a decimal amount as integer cents. The logic lives in <see cref="MoneyConversion"/>.
/// </summary>
public sealed class DecimalToCentsConverter : ValueConverter<decimal, long>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecimalToCentsConverter"/> class.
    /// </summary>
    public DecimalToCentsConverter()
        : base(amount => MoneyConversion.ToCents(amount), cents => MoneyConversion.FromCents(cents))
    {
    }
}
