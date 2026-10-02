using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The authenticated user that makes a request: its identifier and its roles, both taken from the token.
/// </summary>
/// <param name="UserId">The identifier of the user.</param>
/// <param name="Roles">The names of the roles of the user.</param>
public sealed record ExpenseCaller(string UserId, IReadOnlyCollection<string> Roles)
{
    /// <summary>
    /// Checks whether the user has a role.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <returns><c>true</c> when the user has the role.</returns>
    public bool IsInRole(string role)
    {
        return Roles.Any(candidate => string.Equals(candidate, role, StringComparison.Ordinal));
    }
}
