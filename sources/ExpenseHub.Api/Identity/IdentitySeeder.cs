using System;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Creates the required roles and a single initial Admin. Running it again changes nothing that already exists.
/// </summary>
public sealed class IdentitySeeder
{
    private readonly IIdentitySeedStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentitySeeder"/> class.
    /// </summary>
    /// <param name="store">The storage used by the seed.</param>
    public IdentitySeeder(IIdentitySeedStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Seeds the roles and, when no Admin exists yet, the initial Admin account.
    /// The configuration is checked first, so nothing is written when it is incomplete.
    /// </summary>
    /// <param name="adminEmail">The e-mail address of the initial Admin.</param>
    /// <param name="adminPassword">The password of the initial Admin.</param>
    /// <returns>A task that completes when the seed is done.</returns>
    /// <exception cref="InvalidOperationException">The Admin e-mail or password is not configured.</exception>
    public async Task SeedAsync(string? adminEmail, string? adminPassword)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            throw new InvalidOperationException(
                $"'{AdminSeedOptions.SectionName}:Email' is not configured. Set it in the configuration or with the environment variable 'Seed__Admin__Email'.");
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                $"'{AdminSeedOptions.SectionName}:Password' is not configured. Set it with 'dotnet user-secrets' or with the environment variable 'Seed__Admin__Password'.");
        }

        foreach (string role in AppRoles.All)
        {
            if (!await _store.RoleExistsAsync(role))
            {
                await _store.CreateRoleAsync(role);
            }
        }

        if (await _store.AnyUserInRoleAsync(AppRoles.Admin))
        {
            return;
        }

        await _store.CreateUserInRoleAsync(adminEmail, adminPassword, AppRoles.Admin);
    }
}
