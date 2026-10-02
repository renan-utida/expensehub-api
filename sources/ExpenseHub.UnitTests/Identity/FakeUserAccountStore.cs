using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Hand-written in-memory fake of <see cref="IUserAccountStore"/>; it never touches a database.
/// Like the real store, it refuses to put a user in a role that does not exist, so a test fails if the service
/// ever tried to create a role implicitly.
/// </summary>
internal sealed class FakeUserAccountStore : IUserAccountStore
{
    private readonly List<FakeUser> _users = new();

    /// <summary>Gets how many users were created through <see cref="CreateUserAsync"/>.</summary>
    public int CreateUserCalls { get; private set; }

    /// <summary>Gets how many times the roles of a user were replaced.</summary>
    public int ReplaceRolesCalls { get; private set; }

    /// <summary>Adds a user directly, without counting it as a registration.</summary>
    /// <param name="email">The e-mail address of the user.</param>
    /// <param name="roles">The roles the user already has.</param>
    /// <returns>The identifier of the user.</returns>
    public string GivenUser(string email, params string[] roles)
    {
        var user = new FakeUser(Guid.NewGuid().ToString(), email, roles);
        _users.Add(user);
        return user.Id;
    }

    /// <summary>Gets the roles a user currently has.</summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns>The role names.</returns>
    public IReadOnlyList<string> RolesOf(string userId)
    {
        return _users.Single(user => user.Id == userId).Roles.ToList();
    }

    /// <summary>Gets how many users exist.</summary>
    /// <returns>The number of users.</returns>
    public int UserCount()
    {
        return _users.Count;
    }

    /// <inheritdoc />
    public Task<RegistrationResult> CreateUserAsync(string email, string password)
    {
        CreateUserCalls++;

        if (_users.Any(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new RegistrationResult(RegistrationStatus.DuplicateEmail, null, Array.Empty<string>()));
        }

        var created = new FakeUser(Guid.NewGuid().ToString(), email, Array.Empty<string>());
        _users.Add(created);

        return Task.FromResult(new RegistrationResult(RegistrationStatus.Created, ToAccount(created), Array.Empty<string>()));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UserAccount>> ListUsersAsync()
    {
        IReadOnlyList<UserAccount> accounts = _users.OrderBy(user => user.Email).Select(ToAccount).ToList();
        return Task.FromResult(accounts);
    }

    /// <inheritdoc />
    public Task<UserAccount?> FindUserAsync(string userId)
    {
        FakeUser? user = _users.SingleOrDefault(candidate => candidate.Id == userId);
        return Task.FromResult(user is null ? null : ToAccount(user));
    }

    /// <inheritdoc />
    public Task ReplaceRolesAsync(string userId, IReadOnlyCollection<string> roleNames)
    {
        ReplaceRolesCalls++;

        string? unknown = roleNames.FirstOrDefault(name => !AppRoles.All.Contains(name));

        if (unknown is not null)
        {
            throw new InvalidOperationException($"The role '{unknown}' does not exist and is not created implicitly.");
        }

        FakeUser user = _users.Single(candidate => candidate.Id == userId);
        user.Roles.Clear();
        user.Roles.AddRange(roleNames);

        return Task.CompletedTask;
    }

    private static UserAccount ToAccount(FakeUser user)
    {
        return new UserAccount(user.Id, user.Email, user.Roles.OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    private sealed class FakeUser
    {
        public FakeUser(string id, string email, IEnumerable<string> roles)
        {
            Id = id;
            Email = email;
            Roles = roles.ToList();
        }

        public string Id { get; }

        public string Email { get; }

        public List<string> Roles { get; }
    }
}
