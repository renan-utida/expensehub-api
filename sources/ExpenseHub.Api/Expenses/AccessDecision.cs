namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The answer of the access rule to an action on an expense, and the status of the HTTP answer it maps to.
/// </summary>
public enum AccessDecision
{
    /// <summary>The action is allowed.</summary>
    Allowed,

    /// <summary>The expense is missing or outside the read scope of the user: <c>404</c>.</summary>
    NotFound,

    /// <summary>The user lacks the role, or the ownership rule forbids the action: <c>403</c>.</summary>
    Forbidden,

    /// <summary>The state of the expense does not accept the action, or it was already done: <c>409</c>.</summary>
    WrongState,
}
