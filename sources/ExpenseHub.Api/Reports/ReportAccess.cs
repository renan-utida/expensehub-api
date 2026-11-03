using System;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Identity;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The authorization matrix of requesting a report, as one plain rule that does not depend on the web layer or on EF Core:
/// <c>Employee</c> may ask for <c>Mine</c>, <c>Auditor</c> may ask for <c>Global</c>, and a user with both roles may ask for
/// both. <c>Admin</c>, <c>Approver</c> and <c>Finance</c> add nothing, so a user with only those roles may ask for nothing.
/// Roles accumulate and are compared by their exact names.
/// <para>
/// The matrix is about requesting only. Listing, reading and downloading need no role: they are limited to the requests of the
/// caller by ownership, even for an <c>Auditor</c>, whose global privilege is over the data of the report and never over the
/// reports of other users.
/// </para>
/// <para>
/// The answers follow a fixed order: <c>401</c> without a token, <c>403</c> when no role may request a report
/// (<see cref="HasRequestRole"/>), <c>400</c> when the data is invalid, and <c>403</c> when the role may request reports but
/// not this scope (<see cref="CanRequest"/>).
/// </para>
/// </summary>
public static class ReportAccess
{
    /// <summary>
    /// Checks whether the user has any role that may request a report, whatever the scope. A user without such a role gets
    /// <c>403</c> before the data of the request is even validated.
    /// </summary>
    /// <param name="caller">The authenticated user.</param>
    /// <returns><c>true</c> when the user is an <c>Employee</c> or an <c>Auditor</c>.</returns>
    public static bool HasRequestRole(ExpenseCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return caller.IsInRole(AppRoles.Employee) || caller.IsInRole(AppRoles.Auditor);
    }

    /// <summary>
    /// Checks whether the user may request a report of a scope: <c>Mine</c> needs the <c>Employee</c> role and <c>Global</c>
    /// needs the <c>Auditor</c> role. A scope that is not one of the two is never allowed.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="scope">The scope the user asked for.</param>
    /// <returns><c>true</c> when the roles of the user allow the scope.</returns>
    public static bool CanRequest(ExpenseCaller caller, ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return scope switch
        {
            ReportScope.Mine => caller.IsInRole(AppRoles.Employee),
            ReportScope.Global => caller.IsInRole(AppRoles.Auditor),
            _ => false,
        };
    }
}
