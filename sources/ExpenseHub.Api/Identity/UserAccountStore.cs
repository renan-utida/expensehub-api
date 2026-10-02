using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// EF Core and ASP.NET Core Identity implementation of <see cref="IUserAccountStore"/>.
/// </summary>
public sealed class UserAccountStore : IUserAccountStore
{
    private readonly ExpenseHubDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAccountStore"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core context.</param>
    /// <param name="userManager">The Identity user manager.</param>
    public UserAccountStore(ExpenseHubDbContext dbContext, UserManager<IdentityUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<RegistrationResult> CreateUserAsync(string email, string password)
    {
        var user = new IdentityUser { UserName = email, Email = email };

        IdentityResult result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            var created = new UserAccount(user.Id, user.Email, Array.Empty<string>());
            return new RegistrationResult(RegistrationStatus.Created, created, Array.Empty<string>());
        }

        string[] codes = result.Errors.Select(error => error.Code).ToArray();
        bool isDuplicate = codes.Any(code => code is nameof(IdentityErrorDescriber.DuplicateUserName)
            or nameof(IdentityErrorDescriber.DuplicateEmail));

        return isDuplicate
            ? new RegistrationResult(RegistrationStatus.DuplicateEmail, null, Array.Empty<string>())
            : new RegistrationResult(RegistrationStatus.Invalid, null, codes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAccount>> ListUsersAsync()
    {
        var users = await _dbContext.Users
            .OrderBy(user => user.Email)
            .Select(user => new { user.Id, user.Email })
            .ToListAsync();

        var links = await (
            from link in _dbContext.UserRoles
            join role in _dbContext.Roles on link.RoleId equals role.Id
            select new { link.UserId, role.Name })
            .ToListAsync();

        ILookup<string, string> rolesByUser = links.ToLookup(link => link.UserId, link => link.Name ?? string.Empty);

        return users
            .Select(user => new UserAccount(user.Id, user.Email, SortRoles(rolesByUser[user.Id])))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<UserAccount?> FindUserAsync(string userId)
    {
        var user = await _dbContext.Users
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.Id, candidate.Email })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return null;
        }

        List<string> roleNames = await (
            from link in _dbContext.UserRoles
            join role in _dbContext.Roles on link.RoleId equals role.Id
            where link.UserId == userId
            select role.Name ?? string.Empty)
            .ToListAsync();

        return new UserAccount(user.Id, user.Email, SortRoles(roleNames));
    }

    /// <inheritdoc />
    public async Task ReplaceRolesAsync(string userId, IReadOnlyCollection<string> roleNames)
    {
        IdentityUser user = await _dbContext.Users.FirstAsync(candidate => candidate.Id == userId);

        Dictionary<string, string> roleIdsByName = await _dbContext.Roles
            .Where(role => role.Name != null)
            .ToDictionaryAsync(role => role.Name!, role => role.Id);

        var desiredRoleIds = new List<string>();

        foreach (string roleName in roleNames)
        {
            if (!roleIdsByName.TryGetValue(roleName, out string? roleId))
            {
                throw new InvalidOperationException($"The role '{roleName}' does not exist and is not created implicitly.");
            }

            desiredRoleIds.Add(roleId);
        }

        List<IdentityUserRole<string>> currentLinks = await _dbContext.UserRoles
            .Where(link => link.UserId == userId)
            .ToListAsync();

        _dbContext.UserRoles.RemoveRange(currentLinks.Where(link => !desiredRoleIds.Contains(link.RoleId)));

        foreach (string roleId in desiredRoleIds.Except(currentLinks.Select(link => link.RoleId)))
        {
            _dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = roleId });
        }

        user.SecurityStamp = Guid.NewGuid().ToString();

        await _dbContext.SaveChangesAsync();
    }

    private static List<string> SortRoles(IEnumerable<string> roleNames)
    {
        return roleNames.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }
}
