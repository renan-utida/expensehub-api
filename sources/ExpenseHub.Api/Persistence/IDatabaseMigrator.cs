using System.Threading;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Applies the pending migrations of the database. It sits behind an interface so the startup rule can be tested without a
/// database.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Applies the migrations that the database does not have yet. It creates the database when it does not exist and never
    /// removes data.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The number of migrations that were pending and have been applied.</returns>
    Task<int> MigrateAsync(CancellationToken cancellationToken);
}
