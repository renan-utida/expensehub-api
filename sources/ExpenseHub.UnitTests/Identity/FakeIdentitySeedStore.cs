using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Hand-written in-memory fake of <see cref="IIdentitySeedStore"/>; it never touches a database.
/// </summary>
internal sealed class FakeIdentitySeedStore : IIdentitySeedStore
{
    private readonly HashSet<string> _roles = new();
    private readonly List<(string Email, string Role)> _users = new();

    /// <summary>Gets the names of the roles that exist.</summary>
    public IReadOnlyCollection<string> Roles => _roles;

    /// <summary>Gets the users that exist, with the role of each one.</summary>
    public IReadOnlyList<(string Email, string Role)> Users => _users;

    /// <summary>Gets how many times a role was created.</summary>
    public int CreateRoleCalls { get; private set; }

    /// <summary>Gets how many times a user was created.</summary>
    public int CreateUserCalls { get; private set; }

    /// <summary>Adds a role without counting it as a creation made by the seed.</summary>
    /// <param name="roleName">The name of the role.</param>
    public void GivenRole(string roleName)
    {
        _roles.Add(roleName);
    }

    /// <summary>Adds a user without counting it as a creation made by the seed.</summary>
    /// <param name="email">The e-mail address of the user.</param>
    /// <param name="roleName">The role of the user.</param>
    public void GivenUser(string email, string roleName)
    {
        _users.Add((email, roleName));
    }

    /// <inheritdoc />
    public Task<bool> RoleExistsAsync(string roleName)
    {
        return Task.FromResult(_roles.Contains(roleName));
    }

    /// <inheritdoc />
    public Task CreateRoleAsync(string roleName)
    {
        CreateRoleCalls++;
        _roles.Add(roleName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> AnyUserInRoleAsync(string roleName)
    {
        return Task.FromResult(_users.Any(user => user.Role == roleName));
    }

    /// <inheritdoc />
    public Task CreateUserInRoleAsync(string email, string password, string roleName)
    {
        CreateUserCalls++;
        _users.Add((email, roleName));
        return Task.CompletedTask;
    }
}
