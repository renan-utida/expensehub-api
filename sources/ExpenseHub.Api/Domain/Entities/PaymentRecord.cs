using System;

namespace ExpenseHub.Api.Domain.Entities;

/// <summary>
/// Simulated payment of an approved expense.
/// </summary>
public class PaymentRecord
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the identifier of the paid expense.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>Gets or sets the expense that was paid.</summary>
    public Expense Expense { get; set; } = null!;

    /// <summary>Gets or sets the Identity user id of who registered the payment, taken from the token.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC instant of the payment, set by the server.</summary>
    public DateTimeOffset PaidAtUtc { get; set; }
}
