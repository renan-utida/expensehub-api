using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Creates the context for the <c>dotnet ef</c> tooling without starting the web host.
/// </summary>
public sealed class ExpenseHubDbContextFactory : IDesignTimeDbContextFactory<ExpenseHubDbContext>
{
    /// <inheritdoc />
    public ExpenseHubDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = PersistenceServiceCollectionExtensions.ResolveConnectionString(configuration, basePath);

        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new ExpenseHubDbContext(options);
    }
}
