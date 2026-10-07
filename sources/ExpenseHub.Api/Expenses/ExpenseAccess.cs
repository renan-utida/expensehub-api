using System;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Identity;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The authorization matrix of the expenses as one plain rule: it combines the role, the ownership and the state,
/// and does not depend on the web layer or on EF Core. An attribute on an endpoint only keeps out users who have no
/// chance of doing the action; the decision on a given expense is always made here, in the service layer.
/// <para>
/// Every action that writes (edit, submit, approve, reject and pay) is decided the same way, in a fixed order: the role
/// first (<c>403</c>), then whether the expense exists (<c>404</c>, only for an expense that does not exist), then the
/// ownership rule (<c>403</c>) and finally the state (<c>409</c>). The read scope of the user is not used here: it only
/// decides what a user can list and read, where an expense outside it is also a <c>404</c>.
/// </para>
/// <para>
/// Ownership: only the owner edits and submits; nobody approves, rejects or pays their own expense, even when the user
/// also has the <c>Employee</c> role. <c>Admin</c> and <c>Auditor</c> have no role for any of the actions.
/// </para>
/// </summary>
public static class ExpenseAccess
{
    /// <summary>
    /// Checks whether the user has the role the action needs. Roles accumulate, so one matching role is enough.
    /// </summary>
    /// <param name="caller">The authenticated user.</param>
    /// <param name="action">The action.</param>
    /// <returns><c>true</c> when the user has the role of the action.</returns>
    public static bool HasRoleFor(ExpenseCaller caller, ExpenseAction action)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return caller.IsInRole(RequiredRole(action));
    }

    /// <summary>
    /// Decides whether the user can do the action on the expense.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="action">The action.</param>
    /// <param name="expense">The expense, or <c>null</c> when it does not exist or when the action creates one.</param>
    /// <returns>The decision.</returns>
    public static AccessDecision Evaluate(ExpenseCaller caller, ExpenseAction action, Expense? expense)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!HasRoleFor(caller, action))
        {
            return AccessDecision.Forbidden;
        }

        if (action == ExpenseAction.Create)
        {
            return AccessDecision.Allowed;
        }

        if (expense is null)
        {
            return AccessDecision.NotFound;
        }

        bool isOwner = string.Equals(expense.OwnerId, caller.UserId, StringComparison.Ordinal);

        if (isOwner != OwnerMustDoIt(action))
        {
            return AccessDecision.Forbidden;
        }

        return expense.Status == RequiredStatus(action) ? AccessDecision.Allowed : AccessDecision.WrongState;
    }

    private static string RequiredRole(ExpenseAction action)
    {
        return action switch
        {
            ExpenseAction.Create or ExpenseAction.Edit or ExpenseAction.Submit => AppRoles.Employee,
            ExpenseAction.Approve or ExpenseAction.Reject => AppRoles.Approver,
            ExpenseAction.Pay => AppRoles.Finance,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown action."),
        };
    }

    private static bool OwnerMustDoIt(ExpenseAction action)
    {
        return action is ExpenseAction.Edit or ExpenseAction.Submit;
    }

    private static ExpenseStatus RequiredStatus(ExpenseAction action)
    {
        return action switch
        {
            ExpenseAction.Edit or ExpenseAction.Submit => ExpenseStatus.Draft,
            ExpenseAction.Approve or ExpenseAction.Reject => ExpenseStatus.Submitted,
            ExpenseAction.Pay => ExpenseStatus.Approved,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown action."),
        };
    }
}
