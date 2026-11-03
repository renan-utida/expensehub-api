using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Applies the migrations with Entity Framework Core, in its own scope. Running it again when nothing is pending changes nothing.
/// </summary>
public sealed class EfDatabaseMigrator : IDatabaseMigrator
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfDatabaseMigrator"/> class.
    /// </summary>
    /// <param name="scopeFactory">Creates the scope that holds the database context.</param>
    public EfDatabaseMigrator(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ExpenseHubDbContext context = scope.ServiceProvider.GetRequiredService<ExpenseHubDbContext>();

        int pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();

        await context.Database.MigrateAsync(cancellationToken);

        return pending;
    }
}
