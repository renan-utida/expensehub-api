using System.Threading.Tasks;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Storage operations the identity seed needs, kept behind an interface so the seed rules can be tested without a database.
/// </summary>
public interface IIdentitySeedStore
{
    /// <summary>Checks whether a role exists.</summary>
    /// <param name="roleName">The name of the role.</param>
    /// <returns><c>true</c> when the role exists.</returns>
    Task<bool> RoleExistsAsync(string roleName);

    /// <summary>Creates a role.</summary>
    /// <param name="roleName">The name of the role.</param>
    /// <returns>A task that completes when the role is created.</returns>
    Task CreateRoleAsync(string roleName);

    /// <summary>Checks whether any user belongs to a role.</summary>
    /// <param name="roleName">The name of the role.</param>
    /// <returns><c>true</c> when at least one user is in the role.</returns>
    Task<bool> AnyUserInRoleAsync(string roleName);

    /// <summary>Creates a user and puts it in a role, all or nothing.</summary>
    /// <param name="email">The e-mail address, also used as the user name.</param>
    /// <param name="password">The password of the user.</param>
    /// <param name="roleName">The role of the user.</param>
    /// <returns>A task that completes when the user is created.</returns>
    Task CreateUserInRoleAsync(string email, string password, string roleName);
}
