using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Rules of registration and of the administration of user roles.
/// </summary>
public sealed class UserAccountService
{
    private const int MaxReportedNameLength = 64;

    private readonly IUserAccountStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAccountService"/> class.
    /// </summary>
    /// <param name="store">The storage used by the service.</param>
    public UserAccountService(IUserAccountStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Registers a user. The new user never has a role: only an Admin can grant one afterwards.
    /// </summary>
    /// <param name="email">The e-mail address of the user.</param>
    /// <param name="password">The password of the user.</param>
    /// <returns>The outcome of the registration.</returns>
    public Task<RegistrationResult> RegisterAsync(string email, string password)
    {
        return _store.CreateUserAsync(email, password);
    }

    /// <summary>
    /// Lists the users with their roles.
    /// </summary>
    /// <returns>The users.</returns>
    public Task<IReadOnlyList<UserAccount>> ListAsync()
    {
        return _store.ListUsersAsync();
    }

    /// <summary>
    /// Replaces the roles of a user with the requested ones. Unknown roles are rejected and never created,
    /// and an Admin cannot remove their own Admin role, so the system always keeps an Admin.
    /// </summary>
    /// <param name="actorId">The identifier of the authenticated user that makes the change, taken from the token.</param>
    /// <param name="userId">The identifier of the user whose roles change.</param>
    /// <param name="requestedRoles">The names of the roles the user must end up with.</param>
    /// <returns>The outcome of the change.</returns>
    public async Task<UpdateRolesResult> UpdateRolesAsync(
        string? actorId,
        string userId,
        IReadOnlyCollection<string>? requestedRoles)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (requestedRoles is null)
        {
            return new UpdateRolesResult(UpdateRolesStatus.InvalidRoles, null, Array.Empty<string>());
        }

        var desiredRoles = new List<string>();
        var invalidRoles = new List<string>();

        foreach (string requested in requestedRoles)
        {
            string? known = FindKnownRole(requested);

            if (known is null)
            {
                invalidRoles.Add(Describe(requested));
            }
            else if (!desiredRoles.Contains(known))
            {
                desiredRoles.Add(known);
            }
        }

        if (invalidRoles.Count > 0)
        {
            return new UpdateRolesResult(UpdateRolesStatus.InvalidRoles, null, invalidRoles);
        }

        UserAccount? user = await _store.FindUserAsync(userId);

        if (user is null)
        {
            return new UpdateRolesResult(UpdateRolesStatus.UserNotFound, null, Array.Empty<string>());
        }

        bool isOwnAccount = string.Equals(actorId, userId, StringComparison.Ordinal);

        if (isOwnAccount && user.Roles.Contains(AppRoles.Admin) && !desiredRoles.Contains(AppRoles.Admin))
        {
            return new UpdateRolesResult(UpdateRolesStatus.CannotRemoveOwnAdminRole, null, Array.Empty<string>());
        }

        if (user.Roles.Count == desiredRoles.Count && user.Roles.All(desiredRoles.Contains))
        {
            return new UpdateRolesResult(UpdateRolesStatus.Updated, user, Array.Empty<string>());
        }

        await _store.ReplaceRolesAsync(userId, desiredRoles);

        UserAccount? updated = await _store.FindUserAsync(userId);

        return new UpdateRolesResult(UpdateRolesStatus.Updated, updated, Array.Empty<string>());
    }

    private static string? FindKnownRole(string? requested)
    {
        return AppRoles.All.FirstOrDefault(role => string.Equals(role, requested?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string Describe(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return "(empty)";
        }

        return requested.Length <= MaxReportedNameLength ? requested : requested[..MaxReportedNameLength] + "...";
    }
}
