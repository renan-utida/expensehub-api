using System;
using ExpenseHub.Api.Identity;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The read rules of the authorization matrix. Roles accumulate, so the scope of a user is the union of the scopes of its roles.
/// <c>Admin</c> gives no access to expenses by itself.
/// </summary>
public static class ExpenseVisibility
{
    /// <summary>
    /// Works out what a user can read:
    /// <c>Employee</c> reads its own expenses; <c>Approver</c> reads the submitted ones;
    /// <c>Finance</c> reads the approved and paid ones; <c>Auditor</c> reads all of them.
    /// </summary>
    /// <param name="caller">The authenticated user.</param>
    /// <returns>The scope of the user, which is empty when none of its roles reads expenses.</returns>
    public static ExpenseScope ScopeFor(ExpenseCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return new ExpenseScope(
            ownerId: caller.IsInRole(AppRoles.Employee) ? caller.UserId : null,
            seesSubmitted: caller.IsInRole(AppRoles.Approver),
            seesApprovedAndPaid: caller.IsInRole(AppRoles.Finance),
            seesAll: caller.IsInRole(AppRoles.Auditor));
    }
}
