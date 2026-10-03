namespace ExpenseHub.Api.Expenses;

/// <summary>
/// An action a user can try on an expense. Each one has a role, an ownership rule and a state it needs.
/// </summary>
public enum ExpenseAction
{
    /// <summary>Creating a draft.</summary>
    Create,

    /// <summary>Editing a draft.</summary>
    Edit,

    /// <summary>Submitting a draft.</summary>
    Submit,

    /// <summary>Approving a submitted expense.</summary>
    Approve,

    /// <summary>Rejecting a submitted expense.</summary>
    Reject,

    /// <summary>Paying an approved expense.</summary>
    Pay,
}
