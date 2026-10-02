using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Public summary of a user returned by the registration and the admin endpoints.
/// It never carries the password hash or any security data.
/// </summary>
/// <param name="Id">The identifier of the user.</param>
/// <param name="Email">The e-mail address of the user.</param>
/// <param name="Roles">The names of the roles of the user.</param>
public sealed record UserSummaryResponse(string Id, string? Email, IReadOnlyList<string> Roles)
{
    /// <summary>
    /// Builds the response of a user.
    /// </summary>
    /// <param name="account">The user.</param>
    /// <returns>The public summary.</returns>
    public static UserSummaryResponse From(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new UserSummaryResponse(account.Id, account.Email, account.Roles);
    }
}
