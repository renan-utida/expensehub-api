namespace ExpenseHub.Api.Domain.Enums;

/// <summary>
/// Lifecycle state of an expense. Only the server sets it.
/// </summary>
public enum ExpenseStatus
{
    /// <summary>Editable draft owned by the employee.</summary>
    Draft,

    /// <summary>Sent for approval.</summary>
    Submitted,

    /// <summary>Approved and waiting for payment.</summary>
    Approved,

    /// <summary>Rejected. Final state.</summary>
    Rejected,

    /// <summary>Paid. Final state.</summary>
    Paid,
}
