using System;
using ExpenseHub.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
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

        services.AddScoped<IUserAccountStore, UserAccountStore>();
        services.AddScoped<UserAccountService>();

        return services;
    }

    /// <summary>
    /// Registers the startup seed of the required roles and the single initial Admin.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubIdentitySeed(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.AddScoped<IIdentitySeedStore, IdentitySeedStore>();
        services.AddScoped<IdentitySeeder>();
        services.AddHostedService<IdentitySeedHostedService>();

        return services;
    }
}
