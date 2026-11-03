using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Deployment;
using ExpenseHub.UnitTests.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Tests of the line that tells which image is running. It must name the process and the commit, say unknown when the commit is
/// missing, and never carry anything else from the configuration.
/// </summary>
[TestClass]
public sealed class VersionLogHostedServiceTests
{
    private const string FullSha = "78bca7d00a785bb37bcedc5b40bcd14adffa2535";

    /// <summary>The start writes one line with the name of the process and the commit.</summary>
    [TestMethod]
    public async Task StartAsync_LogsTheProcessAndTheCommit()
    {
        var logger = new ListLogger<VersionLogHostedService>();
        var service = new VersionLogHostedService("web", Options.Create(new BuildInfoOptions { Commit = FullSha }), logger);

        await service.StartAsync(CancellationToken.None);

        LogLine line = logger.Lines.Single();
        Assert.AreEqual(LogLevel.Information, line.Level);
        Assert.Contains("web", line.Message);
        Assert.Contains(FullSha, line.Message);
        Assert.Contains("Image commit", line.Message);
    }

    /// <summary>Without a commit the line says unknown, so a local run never looks like a published image.</summary>
    [TestMethod]
    public async Task StartAsync_SaysUnknown_WhenThereIsNoCommit()
    {
        var logger = new ListLogger<VersionLogHostedService>();
        var service = new VersionLogHostedService("worker", Options.Create(new BuildInfoOptions()), logger);

        await service.StartAsync(CancellationToken.None);

        Assert.Contains("worker", logger.Lines.Single().Message);
        Assert.Contains(RunningVersion.Unknown, logger.Lines.Single().Message);
    }

    /// <summary>A value that is not a commit id is not written in the log.</summary>
    [TestMethod]
    public async Task StartAsync_DoesNotLogAValueThatIsNotACommit()
    {
        var logger = new ListLogger<VersionLogHostedService>();
        string injected = "78bca7d" + Environment.NewLine + "forged line";
        var service = new VersionLogHostedService("web", Options.Create(new BuildInfoOptions { Commit = injected }), logger);

        await service.StartAsync(CancellationToken.None);

        Assert.DoesNotContain("forged", logger.Lines.Single().Message);
        Assert.Contains(RunningVersion.Unknown, logger.Lines.Single().Message);
    }

    /// <summary>The stop does nothing and finishes at once.</summary>
    [TestMethod]
    public async Task StopAsync_Completes()
    {
        var service = new VersionLogHostedService("web", Options.Create(new BuildInfoOptions()), new ListLogger<VersionLogHostedService>());

        await service.StopAsync(CancellationToken.None);
    }

    /// <summary>A blank process name is rejected, because the line would not say which process started.</summary>
    [TestMethod]
    public void Constructor_RejectsABlankApplicationName()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new VersionLogHostedService(" ", Options.Create(new BuildInfoOptions()), new ListLogger<VersionLogHostedService>()));
    }

    /// <summary>The registration binds the commit from the Build section and starts the line with the given process name.</summary>
    [TestMethod]
    public async Task AddExpenseHubBuildInfo_LogsTheCommitOfTheConfiguration()
    {
        var logger = new ListLogger<VersionLogHostedService>();
        await using ServiceProvider provider = NewProvider(logger, new Dictionary<string, string?> { ["Build:Commit"] = FullSha });

        await provider.GetServices<IHostedService>().Single().StartAsync(CancellationToken.None);

        Assert.Contains(FullSha, logger.Lines.Single().Message);
        Assert.Contains("web", logger.Lines.Single().Message);
    }

    /// <summary>With a connection string and the credentials of the broker in the configuration, the line carries none of them.</summary>
    [TestMethod]
    public async Task AddExpenseHubBuildInfo_NeverLogsTheRestOfTheConfiguration()
    {
        string brokerUser = new('u', 12);
        string brokerCredential = new('c', 14);
        string adminCredential = new('a', 14);
        string connection = "Data Source=" + new string('d', 10) + ".db";
        var logger = new ListLogger<VersionLogHostedService>();
        var values = new Dictionary<string, string?>
        {
            ["Build:Commit"] = FullSha,
            ["ConnectionStrings:ExpenseHub"] = connection,
            ["RabbitMq:UserName"] = brokerUser,
            ["RabbitMq:Password"] = brokerCredential,
            ["Seed:Admin:Password"] = adminCredential,
        };
        await using ServiceProvider provider = NewProvider(logger, values);

        await provider.GetServices<IHostedService>().Single().StartAsync(CancellationToken.None);

        string text = string.Join('\n', logger.Lines.Select(line => line.Message));
        Assert.DoesNotContain(brokerUser, text);
        Assert.DoesNotContain(brokerCredential, text);
        Assert.DoesNotContain(adminCredential, text);
        Assert.DoesNotContain(connection, text);
        Assert.DoesNotContain("Data Source", text);
    }

    /// <summary>A blank process name is rejected by the registration.</summary>
    [TestMethod]
    public void AddExpenseHubBuildInfo_RejectsABlankApplicationName()
    {
        var services = new ServiceCollection();

        Assert.ThrowsExactly<ArgumentException>(() => services.AddExpenseHubBuildInfo(new ConfigurationBuilder().Build(), " "));
    }

    private static ServiceProvider NewProvider(ListLogger<VersionLogHostedService> logger, Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();

        services.AddExpenseHubBuildInfo(configuration, "web");
        services.AddSingleton<ILogger<VersionLogHostedService>>(logger);

        return services.BuildServiceProvider();
    }
}
