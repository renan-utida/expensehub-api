using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of what happens while no real broker publisher is registered: the publisher never confirms, so the API never answers
/// 202 without a message that was really sent, and a real publisher can replace it.
/// </summary>
[TestClass]
public sealed class ReportPublisherRegistrationTests
{
    /// <summary>The publisher used without a real broker never confirms and never throws.</summary>
    [TestMethod]
    public async Task UnavailableReportPublisher_NeverConfirms()
    {
        var publisher = new UnavailableReportPublisher(NullLogger<UnavailableReportPublisher>.Instance);

        ReportPublishResult result = await publisher.PublishAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.AreEqual(ReportPublishResult.NotConfirmed, result);
    }

    /// <summary>By default the registered publisher is the one that never confirms.</summary>
    [TestMethod]
    public void AddExpenseHubReports_RegistersThePublisherThatNeverConfirmsByDefault()
    {
        var services = new ServiceCollection();

        services.AddExpenseHubReports();

        ServiceDescriptor descriptor = services.Single(service => service.ServiceType == typeof(IReportPublisher));
        Assert.AreEqual(typeof(UnavailableReportPublisher), descriptor.ImplementationType);
    }

    /// <summary>A real publisher registered first is kept: the default one does not replace it.</summary>
    [TestMethod]
    public void AddExpenseHubReports_KeepsARealPublisherRegisteredBefore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReportPublisher, FakeReportPublisher>();

        services.AddExpenseHubReports();

        ServiceDescriptor descriptor = services.Single(service => service.ServiceType == typeof(IReportPublisher));
        Assert.AreEqual(typeof(FakeReportPublisher), descriptor.ImplementationType);
    }

    /// <summary>The report service and its repository are registered.</summary>
    [TestMethod]
    public void AddExpenseHubReports_RegistersTheServiceAndTheRepository()
    {
        var services = new ServiceCollection();

        services.AddExpenseHubReports();

        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(ReportService)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IReportRepository)));
    }

    /// <summary>The registration does not accept a missing collection.</summary>
    [TestMethod]
    public void AddExpenseHubReports_MissingCollection_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportsServiceCollectionExtensions.AddExpenseHubReports(null!));
    }
}
