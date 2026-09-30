namespace ExpenseHub.Api.Domain.Enums;

/// <summary>
/// Action recorded in the history of an expense.
/// </summary>
public enum ExpenseHistoryAction
{
    /// <summary>The draft was created.</summary>
    Created,

    /// <summary>The draft was edited.</summary>
    Edited,

    /// <summary>The draft was submitted.</summary>
    Submitted,

    /// <summary>The expense was approved.</summary>
    Approved,

    /// <summary>The expense was rejected.</summary>
    Rejected,

    /// <summary>The expense was paid.</summary>
    Paid,
}
