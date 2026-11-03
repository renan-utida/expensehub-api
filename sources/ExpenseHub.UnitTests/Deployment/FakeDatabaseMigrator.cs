using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Persistence;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Hand-written migrator that applies nothing: it counts the calls and can be told to fail, so the startup rule can be tested
/// without a database.
/// </summary>
internal sealed class FakeDatabaseMigrator : IDatabaseMigrator
{
    /// <summary>Gets how many times the migrator was called.</summary>
    public int Calls { get; private set; }

    /// <summary>Gets the cancellation token of the last call.</summary>
    public CancellationToken LastToken { get; private set; }

    /// <summary>Gets or sets the number of migrations the fake reports as applied.</summary>
    public int Applied { get; set; }

    /// <summary>Gets or sets the failure the fake throws; when null it succeeds.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc />
    public Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        Calls++;
        LastToken = cancellationToken;

        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(Applied);
    }
}
