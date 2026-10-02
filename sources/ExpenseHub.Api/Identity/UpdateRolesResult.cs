using System.Collections.Generic;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// The possible outcomes of replacing the roles of a user.
/// </summary>
public enum UpdateRolesStatus
{
    /// <summary>The roles are now the requested ones.</summary>
    Updated,

    /// <summary>At least one requested role is not a known role.</summary>
    InvalidRoles,

    /// <summary>The user does not exist.</summary>
    UserNotFound,

    /// <summary>An Admin tried to remove their own Admin role.</summary>
    CannotRemoveOwnAdminRole,
}

/// <summary>
/// The result of replacing the roles of a user.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="User">The user with the resulting roles, when the status is <see cref="UpdateRolesStatus.Updated"/>.</param>
/// <param name="InvalidRoles">The requested names that are not known roles, when the status is <see cref="UpdateRolesStatus.InvalidRoles"/>.</param>
public sealed record UpdateRolesResult(UpdateRolesStatus Status, UserAccount? User, IReadOnlyList<string> InvalidRoles);
