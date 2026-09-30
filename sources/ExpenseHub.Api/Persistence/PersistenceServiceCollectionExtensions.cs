using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Registers the persistence of ExpenseHub.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>The name of the connection string in the configuration.</summary>
    public const string ConnectionStringName = "ExpenseHub";

    /// <summary>
    /// Registers <see cref="ExpenseHubDbContext"/> using SQLite.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="basePath">The directory that relative database paths are resolved against.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string basePath)
    {
        string connectionString = ResolveConnectionString(configuration, basePath);

        return services.AddDbContext<ExpenseHubDbContext>(options => options.UseSqlite(connectionString));
    }

    /// <summary>
    /// Reads the connection string and anchors a relative database file to <paramref name="basePath"/>,
    /// so the application and the migration tooling use the same file whatever the working directory is.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="basePath">The directory that relative database paths are resolved against.</param>
    /// <returns>The connection string with an absolute database path.</returns>
    /// <exception cref="InvalidOperationException">The connection string is not configured.</exception>
    public static string ResolveConnectionString(IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(builder.DataSource, basePath);
        }

        return builder.ConnectionString;
    }
}
