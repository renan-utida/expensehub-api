using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Runs <see cref="IdentitySeeder"/> when the application starts. A failure stops the startup.
/// </summary>
public sealed partial class IdentitySeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AdminSeedOptions> _options;
    private readonly ILogger<IdentitySeedHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentitySeedHostedService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Creates the scope that holds the scoped Identity services.</param>
    /// <param name="options">The initial Admin options.</param>
    /// <param name="logger">The logger.</param>
    public IdentitySeedHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AdminSeedOptions> options,
        ILogger<IdentitySeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IdentitySeeder seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync(_options.Value.Email, _options.Value.Password);

        LogSeedCompleted();
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity seed completed.")]
    private partial void LogSeedCompleted();
}
