using System;
using System.IO;
using ExpenseHub.Api.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseHub.Api.Deployment;

/// <summary>
/// Registers what the containers need beyond the application itself: the evidence of the running version, the preparation of
/// the database and the place of the keys of the data protection. None of them changes the behavior when it is not configured.
/// </summary>
public static class DeploymentServiceCollectionExtensions
{
    /// <summary>The fixed name of the application for the data protection, so the keys do not depend on the path of the content.</summary>
    public const string DataProtectionApplicationName = "ExpenseHub";

    /// <summary>
    /// Binds the <c>Build</c> section and logs, when the process starts, which commit the image was built from. The web
    /// application and the worker call it, each with its own name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="application">The name of the process, for example <c>web</c> or <c>worker</c>.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubBuildInfo(
        this IServiceCollection services,
        IConfiguration configuration,
        string application)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(application);

        services.Configure<BuildInfoOptions>(configuration.GetSection(BuildInfoOptions.SectionName));
        services.AddSingleton<IHostedService>(provider => new VersionLogHostedService(
            application,
            provider.GetRequiredService<IOptions<BuildInfoOptions>>(),
            provider.GetRequiredService<ILogger<VersionLogHostedService>>()));

        return services;
    }

    /// <summary>
    /// Registers the step that applies the pending migrations when the application starts. The step does nothing unless
    /// <c>Database:MigrateOnStartup</c> is on. Hosted services start in the order they are registered, so call this before
    /// registering the seed of the Admin.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubDatabaseInitialization(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseInitializationOptions>(configuration.GetSection(DatabaseInitializationOptions.SectionName));
        services.AddSingleton<IDatabaseMigrator, EfDatabaseMigrator>();
        services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }

    /// <summary>
    /// Keeps the keys of the data protection in the directory of <c>DataProtection:KeysPath</c>, when it is set, so the bearer
    /// tokens stay valid after a container is recreated. When it is not set nothing is registered and ASP.NET Core keeps its
    /// default behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="basePath">The directory that a relative path is resolved against.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string basePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new KeyStorageOptions();
        configuration.GetSection(KeyStorageOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.KeysPath))
        {
            return services;
        }

        string keysPath = Path.GetFullPath(options.KeysPath, basePath);

        services
            .AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        return services;
    }
}
