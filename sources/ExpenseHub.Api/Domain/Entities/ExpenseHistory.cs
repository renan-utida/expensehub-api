using System;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Domain.Entities;

/// <summary>
/// One entry in the history of an expense.
/// </summary>
public class ExpenseHistory
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the identifier of the expense.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>Gets or sets the expense this entry belongs to.</summary>
    public Expense Expense { get; set; } = null!;

    /// <summary>Gets or sets the action performed.</summary>
    public ExpenseHistoryAction Action { get; set; }

    /// <summary>Gets or sets the Identity user id of the actor, taken from the token.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC instant of the action, set by the server.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Gets or sets the state before the action. It is null when the expense did not exist yet.</summary>
    public ExpenseStatus? PreviousStatus { get; set; }

    /// <summary>Gets or sets the state after the action.</summary>
    public ExpenseStatus NewStatus { get; set; }

    /// <summary>Gets or sets the rejection reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the summary of the changes made while the expense was a draft.</summary>
    public string? Changes { get; set; }
}
