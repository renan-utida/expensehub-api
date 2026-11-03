using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Deployment;
using ExpenseHub.Api.Persistence;
using ExpenseHub.UnitTests.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Tests of the switch that prepares the database when the web application starts. It is off by default, so the behavior of the
/// CP2 does not change; when it is on, the migrations are applied before the application serves requests, and a failure stops
/// the start. The migrator is a hand-written fake: no database is opened here, and the real migration was checked in the
/// Compose environment.
/// </summary>
[TestClass]
public sealed class DatabaseMigrationTests
{
    /// <summary>The switch is off when nothing is configured.</summary>
    [TestMethod]
    public void MigrateOnStartup_IsOffByDefault()
    {
        Assert.IsFalse(new DatabaseInitializationOptions().MigrateOnStartup);
    }

    /// <summary>With the switch off the migrator is never called, so a default run does not touch the schema.</summary>
    [TestMethod]
    public async Task StartAsync_DoesNotMigrate_WhenTheSwitchIsOff()
    {
        var migrator = new FakeDatabaseMigrator();
        DatabaseMigrationHostedService service = NewService(migrator, migrateOnStartup: false);

        await service.StartAsync(CancellationToken.None);

        Assert.AreEqual(0, migrator.Calls);
    }

    /// <summary>With the switch on the migrator is called once.</summary>
    [TestMethod]
    public async Task StartAsync_Migrates_WhenTheSwitchIsOn()
    {
        var migrator = new FakeDatabaseMigrator();
        DatabaseMigrationHostedService service = NewService(migrator, migrateOnStartup: true);

        await service.StartAsync(CancellationToken.None);

        Assert.AreEqual(1, migrator.Calls);
    }

    /// <summary>The cancellation token of the start reaches the migrator, so a shutdown can interrupt the migration.</summary>
    [TestMethod]
    public async Task StartAsync_PassesTheCancellationTokenToTheMigrator()
    {
        var migrator = new FakeDatabaseMigrator();
        DatabaseMigrationHostedService service = NewService(migrator, migrateOnStartup: true);
        using var source = new CancellationTokenSource();

        await service.StartAsync(source.Token);

        Assert.AreEqual(source.Token, migrator.LastToken);
    }

    /// <summary>The number of migrations that were applied goes to the log.</summary>
    [TestMethod]
    public async Task StartAsync_LogsHowManyMigrationsWereApplied()
    {
        var migrator = new FakeDatabaseMigrator { Applied = 4 };
        var logger = new ListLogger<DatabaseMigrationHostedService>();
        var service = new DatabaseMigrationHostedService(
            Options.Create(new DatabaseInitializationOptions { MigrateOnStartup = true }),
            migrator,
            logger);

        await service.StartAsync(CancellationToken.None);

        LogLine line = logger.Lines.Single(entry => entry.Level == LogLevel.Information);
        Assert.Contains("4", line.Message);
    }

    /// <summary>A failure of the migration is not swallowed: the application does not start with a database it could not prepare.</summary>
    [TestMethod]
    public async Task StartAsync_DoesNotSwallowAFailure()
    {
        var migrator = new FakeDatabaseMigrator { Failure = new InvalidOperationException("migration failed") };
        DatabaseMigrationHostedService service = NewService(migrator, migrateOnStartup: true);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
    }

    /// <summary>The stop does nothing and finishes at once.</summary>
    [TestMethod]
    public async Task StopAsync_Completes()
    {
        DatabaseMigrationHostedService service = NewService(new FakeDatabaseMigrator(), migrateOnStartup: true);

        await service.StopAsync(CancellationToken.None);
    }

    /// <summary>The registration reads the switch from the Database section of the configuration.</summary>
    [TestMethod]
    public async Task AddExpenseHubDatabaseInitialization_BindsTheSwitchFromTheConfiguration()
    {
        await using ServiceProvider on = NewProvider(new Dictionary<string, string?> { ["Database:MigrateOnStartup"] = "true" });
        await using ServiceProvider off = NewProvider(new Dictionary<string, string?>());

        Assert.IsTrue(on.GetRequiredService<IOptions<DatabaseInitializationOptions>>().Value.MigrateOnStartup);
        Assert.IsFalse(off.GetRequiredService<IOptions<DatabaseInitializationOptions>>().Value.MigrateOnStartup);
    }

    /// <summary>The registration adds the step that migrates and the real migrator.</summary>
    [TestMethod]
    public async Task AddExpenseHubDatabaseInitialization_RegistersTheStepAndTheMigrator()
    {
        await using ServiceProvider provider = NewProvider(new Dictionary<string, string?>());

        Assert.IsInstanceOfType<EfDatabaseMigrator>(provider.GetRequiredService<IDatabaseMigrator>());
        Assert.IsInstanceOfType<DatabaseMigrationHostedService>(provider.GetServices<IHostedService>().Single());
    }

    /// <summary>A null service collection or configuration is rejected.</summary>
    [TestMethod]
    public void AddExpenseHubDatabaseInitialization_RejectsNullArguments()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.ThrowsExactly<ArgumentNullException>(() => ((IServiceCollection)null!).AddExpenseHubDatabaseInitialization(configuration));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServiceCollection().AddExpenseHubDatabaseInitialization(null!));
    }

    private static DatabaseMigrationHostedService NewService(FakeDatabaseMigrator migrator, bool migrateOnStartup)
    {
        return new DatabaseMigrationHostedService(
            Options.Create(new DatabaseInitializationOptions { MigrateOnStartup = migrateOnStartup }),
            migrator,
            new ListLogger<DatabaseMigrationHostedService>());
    }

    private static ServiceProvider NewProvider(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddExpenseHubDatabaseInitialization(configuration);

        return services.BuildServiceProvider();
    }
}
