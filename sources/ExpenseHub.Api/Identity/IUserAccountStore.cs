using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Storage operations the user administration needs, kept behind an interface so its rules can be tested without a database.
/// </summary>
public interface IUserAccountStore
{
    /// <summary>Creates a user without any role.</summary>
    /// <param name="email">The e-mail address, also used as the user name.</param>
    /// <param name="password">The password of the user.</param>
    /// <returns>The outcome, with the created user when it worked.</returns>
    Task<RegistrationResult> CreateUserAsync(string email, string password);

    /// <summary>Lists every user with its roles, ordered by e-mail.</summary>
    /// <returns>The users.</returns>
    Task<IReadOnlyList<UserAccount>> ListUsersAsync();

    /// <summary>Finds a user with its roles.</summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>The user, or <c>null</c> when it does not exist.</returns>
    Task<UserAccount?> FindUserAsync(string userId);

    /// <summary>
    /// Replaces the roles of a user in a single atomic write and renews the security stamp of the user,
    /// so tokens issued before the change stop being accepted. Never creates a role.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roleNames">The exact names of the roles the user must end up with.</param>
    /// <returns>A task that completes when the roles are saved.</returns>
    Task ReplaceRolesAsync(string userId, IReadOnlyCollection<string> roleNames);
}
