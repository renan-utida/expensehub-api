using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of how the real publisher is wired in: it is registered as the publisher of the report messages whether it comes before
/// or after the default one, the settings are read from the configuration, and a publisher without settings refuses to publish
/// without opening any connection. Nothing here reaches a network or a broker.
/// </summary>
[TestClass]
public sealed class MessagingRegistrationTests
{
    /// <summary>The messaging registration makes the RabbitMQ publisher the publisher of the report messages.</summary>
    [TestMethod]
    public void AddExpenseHubMessaging_RegistersTheRabbitMqPublisher()
    {
        var services = new ServiceCollection();

        services.AddExpenseHubMessaging(NewConfiguration());

        ServiceDescriptor descriptor = services.Single(service => service.ServiceType == typeof(IReportPublisher));
        Assert.AreEqual(typeof(RabbitMqReportPublisher), descriptor.ImplementationType);
        Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>Registered before the reports, the RabbitMQ publisher is kept: the default publisher that never confirms does not replace it.</summary>
    [TestMethod]
    public void Registered_BeforeTheReports_TheRabbitMqPublisherIsKept()
    {
        var services = new ServiceCollection();

        services.AddExpenseHubMessaging(NewConfiguration());
        services.AddExpenseHubReports();

        ServiceDescriptor descriptor = services.Single(service => service.ServiceType == typeof(IReportPublisher));
        Assert.AreEqual(typeof(RabbitMqReportPublisher), descriptor.ImplementationType);
    }

    /// <summary>Registered after the reports, the RabbitMQ publisher replaces the default one, so the order never leaves the publisher that never confirms.</summary>
    [TestMethod]
    public void Registered_AfterTheReports_TheRabbitMqPublisherReplacesTheDefault()
    {
        var services = new ServiceCollection();

        services.AddExpenseHubReports();
        services.AddExpenseHubMessaging(NewConfiguration());

        ServiceDescriptor descriptor = services.Single(service => service.ServiceType == typeof(IReportPublisher));
        Assert.AreEqual(typeof(RabbitMqReportPublisher), descriptor.ImplementationType);
    }

    /// <summary>The publisher is built from the container without opening a connection, so the application starts even when the broker is down.</summary>
    [TestMethod]
    public async Task Publisher_IsResolvedWithoutOpeningAConnection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExpenseHubMessaging(NewConfiguration());
        await using ServiceProvider provider = services.BuildServiceProvider();

        IReportPublisher publisher = provider.GetRequiredService<IReportPublisher>();

        Assert.IsInstanceOfType<RabbitMqReportPublisher>(publisher);
        Assert.AreSame(publisher, provider.GetRequiredService<IReportPublisher>());
    }

    /// <summary>The settings are bound from the RabbitMq section of the configuration.</summary>
    [TestMethod]
    public async Task Options_AreBoundFromTheConfiguration()
    {
        string userName = new('u', 8);
        string credential = new('p', 12);
        IConfiguration configuration = NewConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "broker.test",
            ["RabbitMq:Port"] = "5673",
            ["RabbitMq:VirtualHost"] = "expensehub",
            ["RabbitMq:QueueName"] = "reports.test",
            ["RabbitMq:UserName"] = userName,
            ["RabbitMq:Password"] = credential,
            ["RabbitMq:PublishTimeoutSeconds"] = "3",
            ["RabbitMq:TransientRetryDelaySeconds"] = "7",
        });
        var services = new ServiceCollection();
        services.AddExpenseHubRabbitMqOptions(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();

        RabbitMqOptions options = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        Assert.AreEqual("broker.test", options.Host);
        Assert.AreEqual(5673, options.Port);
        Assert.AreEqual("expensehub", options.VirtualHost);
        Assert.AreEqual("reports.test", options.QueueName);
        Assert.AreEqual(userName, options.UserName);
        Assert.AreEqual(credential, options.Password);
        Assert.AreEqual(3, options.PublishTimeoutSeconds);
        Assert.AreEqual(7, options.TransientRetryDelaySeconds);
        Assert.IsEmpty(options.Validate());
    }

    /// <summary>Registering only the settings, as the worker does, does not register a publisher.</summary>
    [TestMethod]
    public void AddExpenseHubRabbitMqOptions_DoesNotRegisterAPublisher()
    {
        var services = new ServiceCollection();

        services.AddExpenseHubRabbitMqOptions(NewConfiguration());

        Assert.IsFalse(services.Any(service => service.ServiceType == typeof(IReportPublisher)));
    }

    /// <summary>Without the credentials the publisher refuses to publish and says so, before opening any connection; the service turns this into the 503 with the identifier.</summary>
    [TestMethod]
    public async Task PublishAsync_WithoutSettings_ThrowsBeforeOpeningAnyConnection()
    {
        await using var publisher = new RabbitMqReportPublisher(Options.Create(new RabbitMqOptions()), NullLogger<RabbitMqReportPublisher>.Instance);

        ReportPublishException exception = await Assert.ThrowsAsync<ReportPublishException>(() => publisher.PublishAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.IsNull(exception.InnerException);
        Assert.DoesNotContain("localhost", exception.Message);
    }

    /// <summary>A publisher that never connected can be disposed, and more than once.</summary>
    [TestMethod]
    public async Task DisposeAsync_WithoutAConnection_IsSafe()
    {
        var publisher = new RabbitMqReportPublisher(Options.Create(new RabbitMqOptions()), NullLogger<RabbitMqReportPublisher>.Instance);

        await publisher.DisposeAsync();
        await publisher.DisposeAsync();
    }

    /// <summary>The registrations do not accept missing arguments.</summary>
    [TestMethod]
    public void Registrations_MissingArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => MessagingServiceCollectionExtensions.AddExpenseHubMessaging(null!, NewConfiguration()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddExpenseHubMessaging(null!));
        Assert.Throws<ArgumentNullException>(() => MessagingServiceCollectionExtensions.AddExpenseHubRabbitMqOptions(null!, NewConfiguration()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddExpenseHubRabbitMqOptions(null!));
    }

    private static IConfiguration NewConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values ?? new Dictionary<string, string?>()).Build();
    }
}
