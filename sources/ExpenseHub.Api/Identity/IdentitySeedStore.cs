using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// ASP.NET Core Identity implementation of <see cref="IIdentitySeedStore"/>.
/// Failures report only the Identity error codes, never the password.
/// </summary>
public sealed class IdentitySeedStore : IIdentitySeedStore
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentitySeedStore"/> class.
    /// </summary>
    /// <param name="roleManager">The Identity role manager.</param>
    /// <param name="userManager">The Identity user manager.</param>
    public IdentitySeedStore(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public Task<bool> RoleExistsAsync(string roleName)
    {
        return _roleManager.RoleExistsAsync(roleName);
    }

    /// <inheritdoc />
    public async Task CreateRoleAsync(string roleName)
    {
        IdentityResult result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        EnsureSucceeded(result, $"create the role '{roleName}'");
    }

    /// <inheritdoc />
    public async Task<bool> AnyUserInRoleAsync(string roleName)
    {
        IList<IdentityUser> users = await _userManager.GetUsersInRoleAsync(roleName);
        return users.Count > 0;
    }

    /// <inheritdoc />
    public async Task CreateUserInRoleAsync(string email, string password, string roleName)
    {
        var user = new IdentityUser { UserName = email, Email = email };

        IdentityResult created = await _userManager.CreateAsync(user, password);
        EnsureSucceeded(created, "create the initial Admin user");

        IdentityResult assigned = await _userManager.AddToRoleAsync(user, roleName);

        if (!assigned.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            EnsureSucceeded(assigned, $"add the initial Admin user to the role '{roleName}'");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            string codes = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Could not {action}: {codes}.");
        }
    }
}
