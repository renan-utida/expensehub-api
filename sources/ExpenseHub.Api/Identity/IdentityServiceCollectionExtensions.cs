using System;
using ExpenseHub.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Registers ASP.NET Core Identity and the bearer authentication of ExpenseHub.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers Identity with EF Core stores, roles, the sign-in manager and the Identity bearer scheme.
    /// The built-in <c>MapIdentityApi</c> endpoints are not used, so registration and login stay under our control.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubIdentity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);

        services.AddAuthorization();

        services
            .AddIdentityCore<IdentityUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>()
            .AddSignInManager();

        return services;
    }
}
