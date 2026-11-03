using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Applies the pending migrations when the web application starts, but only when
/// <see cref="DatabaseInitializationOptions.MigrateOnStartup"/> is on. It must be registered before the seed of the Admin, which
/// needs the tables. A failure is not swallowed: the application does not start with a database it could not prepare.
/// </summary>
public sealed partial class DatabaseMigrationHostedService : IHostedService
{
    private readonly IOptions<DatabaseInitializationOptions> _options;
    private readonly IDatabaseMigrator _migrator;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMigrationHostedService"/> class.
    /// </summary>
    /// <param name="options">How the database is prepared at startup.</param>
    /// <param name="migrator">Applies the migrations.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseMigrationHostedService(
        IOptions<DatabaseInitializationOptions> options,
        IDatabaseMigrator migrator,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(migrator);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _migrator = migrator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.MigrateOnStartup)
        {
            LogDisabled();
            return;
        }

        int applied = await _migrator.MigrateAsync(cancellationToken);

        LogApplied(applied);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database migration on startup is off.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied on startup: {Count} pending migration(s).")]
    private partial void LogApplied(int count);
}
