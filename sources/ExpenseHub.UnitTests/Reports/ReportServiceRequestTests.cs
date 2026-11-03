using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of requesting a report: what is saved and published for a valid request, that invalid data saves and publishes
/// nothing, that the requester always comes from the authenticated user, and what happens when the broker does not confirm or
/// the storage fails (the request stays pending and is never marked as failed; no identifier exists when it was not saved).
/// </summary>
[TestClass]
public sealed class ReportServiceRequestTests
{
    /// <summary>A valid request is saved as pending, for the authenticated user, with a new identifier and the instant of the server, and then published once.</summary>
    [TestMethod]
    public async Task RequestAsync_ValidRequest_SavesAPendingReportForTheCallerAndPublishesIt()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester, "Employee"), ReportTestData.Details("Mine"));

        Assert.AreEqual(ReportOperationStatus.Accepted, result.Status);
        ReportRequest saved = repository.Reports.Single();
        Assert.AreSame(saved, result.Report);
        Assert.AreNotEqual(Guid.Empty, saved.Id);
        Assert.AreEqual(ReportTestData.Requester, saved.RequesterId);
        Assert.AreEqual(ReportScope.Mine, saved.Scope);
        Assert.AreEqual(ReportTestData.From, saved.FromDate);
        Assert.AreEqual(ReportTestData.To, saved.ToDate);
        Assert.AreEqual(ReportStatus.Pending, saved.Status);
        Assert.AreEqual(ReportTestData.Now, saved.CreatedAtUtc);
        Assert.IsNull(saved.StartedAtUtc);
        Assert.IsNull(saved.CompletedAtUtc);
        Assert.IsNull(saved.ResultJson);
        Assert.IsNull(saved.ErrorCode);
        CollectionAssert.AreEqual(new[] { saved.Id }, publisher.Published.ToArray());
    }

    /// <summary>The message is published only after the request is saved, so the worker can always find the record.</summary>
    [TestMethod]
    public async Task RequestAsync_PublishesOnlyAfterTheRequestIsSaved()
    {
        (ReportService service, _, FakeReportPublisher publisher) = Create();

        await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), ReportTestData.Details());

        Assert.IsTrue(publisher.SavedAtLastPublish);
    }

    /// <summary>The scope is saved as the client chose it.</summary>
    /// <param name="scopeName">The name of the scope.</param>
    /// <param name="expected">The scope.</param>
    [TestMethod]
    [DataRow("Mine", ReportScope.Mine)]
    [DataRow("Global", ReportScope.Global)]
    public async Task RequestAsync_SavesTheScopeChosen(string scopeName, ReportScope expected)
    {
        (ReportService service, FakeReportRepository repository, _) = Create();

        await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), ReportTestData.Details(scopeName));

        Assert.AreEqual(expected, repository.Reports.Single().Scope);
    }

    /// <summary>The requester is the authenticated user and never anything in the data of the client.</summary>
    [TestMethod]
    public async Task RequestAsync_TheRequesterIsAlwaysTheAuthenticatedUser()
    {
        (ReportService service, FakeReportRepository repository, _) = Create();

        await service.RequestAsync(ReportTestData.Caller("user-from-the-token"), ReportTestData.Details());

        Assert.AreEqual("user-from-the-token", repository.Reports.Single().RequesterId);
        Assert.IsFalse(typeof(ReportRequestDetails).GetProperties().Any(property => property.Name.Contains("Requester", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("Owner", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>A new valid request with the same data creates another report with another identifier: requests are not deduplicated.</summary>
    [TestMethod]
    public async Task RequestAsync_SameDataTwice_CreatesTwoReportsWithDifferentIdentifiers()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        ExpenseCaller caller = ReportTestData.Caller(ReportTestData.Requester);

        ReportOperationResult first = await service.RequestAsync(caller, ReportTestData.Details());
        ReportOperationResult second = await service.RequestAsync(caller, ReportTestData.Details());

        Assert.HasCount(2, repository.Reports);
        Assert.AreNotEqual(first.Report!.Id, second.Report!.Id);
        Assert.HasCount(2, publisher.Published);
    }

    /// <summary>A start date after the end date is rejected: nothing is saved and nothing is published.</summary>
    [TestMethod]
    public async Task RequestAsync_StartAfterEnd_IsRejectedWithoutSavingOrPublishing()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        var details = new ReportRequestDetails(ReportTestData.To, ReportTestData.From, "Mine");

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), details);

        Assert.AreEqual(ReportOperationStatus.ValidationFailed, result.Status);
        Assert.IsNull(result.Report);
        Assert.AreEqual(ReportRules.FromDateField, result.Errors.Single().Field);
        AssertNothingSavedOrPublished(repository, publisher);
    }

    /// <summary>Missing dates are rejected: nothing is saved and nothing is published.</summary>
    [TestMethod]
    public async Task RequestAsync_MissingDates_AreRejectedWithoutSavingOrPublishing()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();

        ReportOperationResult result = await service.RequestAsync(
            ReportTestData.Caller(ReportTestData.Requester),
            new ReportRequestDetails(null, null, "Mine"));

        Assert.AreEqual(ReportOperationStatus.ValidationFailed, result.Status);
        Assert.HasCount(2, result.Errors);
        AssertNothingSavedOrPublished(repository, publisher);
    }

    /// <summary>A scope that is missing, unknown or written as a number is rejected: nothing is saved and nothing is published.</summary>
    /// <param name="scope">The scope sent.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("mine")]
    [DataRow("Everyone")]
    public async Task RequestAsync_InvalidScope_IsRejectedWithoutSavingOrPublishing(string? scope)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), ReportTestData.Details(scope));

        Assert.AreEqual(ReportOperationStatus.ValidationFailed, result.Status);
        Assert.AreEqual(ReportRules.ScopeField, result.Errors.Single().Field);
        AssertNothingSavedOrPublished(repository, publisher);
    }

    /// <summary>When the broker does not confirm, the request is already saved and stays pending, the identifier is given back, and nothing is marked as failed.</summary>
    [TestMethod]
    public async Task RequestAsync_BrokerDoesNotConfirm_KeepsTheSavedReportPendingAndGivesItsIdentifier()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        publisher.Result = ReportPublishResult.NotConfirmed;

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), ReportTestData.Details());

        Assert.AreEqual(ReportOperationStatus.NotPublished, result.Status);
        ReportRequest saved = repository.Reports.Single();
        Assert.AreSame(saved, result.Report);
        Assert.AreEqual(ReportStatus.Pending, saved.Status);
        Assert.IsNull(saved.ErrorCode);
        Assert.IsNull(saved.CompletedAtUtc);
        Assert.IsNull(saved.ResultJson);
    }

    /// <summary>When the broker cannot be reached, the answer is the same as for a message it did not confirm.</summary>
    [TestMethod]
    public async Task RequestAsync_BrokerCannotBeReached_KeepsTheSavedReportPendingAndGivesItsIdentifier()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        publisher.ThrowOnPublish = true;

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), ReportTestData.Details());

        Assert.AreEqual(ReportOperationStatus.NotPublished, result.Status);
        Assert.AreEqual(repository.Reports.Single().Id, result.Report!.Id);
        Assert.AreEqual(ReportStatus.Pending, repository.Reports.Single().Status);
    }

    /// <summary>When the storage fails, there is no report and no identifier, and nothing is published.</summary>
    [TestMethod]
    public async Task RequestAsync_StorageFails_GivesNoReportAndPublishesNothing()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        repository.FailOnAdd = true;

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller(ReportTestData.Requester), ReportTestData.Details());

        Assert.AreEqual(ReportOperationStatus.StorageUnavailable, result.Status);
        Assert.IsNull(result.Report);
        Assert.IsEmpty(repository.Reports);
        Assert.IsEmpty(publisher.Published);
    }

    /// <summary>The service refuses a caller without an identifier, so a request is never saved without a requester.</summary>
    /// <param name="userId">An identifier that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task RequestAsync_CallerWithoutIdentifier_Throws(string? userId)
    {
        (ReportService service, FakeReportRepository repository, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.RequestAsync(ReportTestData.Caller(userId!), ReportTestData.Details()));

        Assert.AreEqual(0, repository.AddCalls);
    }

    private static (ReportService Service, FakeReportRepository Repository, FakeReportPublisher Publisher) Create()
    {
        var repository = new FakeReportRepository();
        var publisher = new FakeReportPublisher(repository);
        var service = new ReportService(repository, publisher, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository, publisher);
    }

    private static void AssertNothingSavedOrPublished(FakeReportRepository repository, FakeReportPublisher publisher)
    {
        Assert.AreEqual(0, repository.AddCalls);
        Assert.IsEmpty(repository.Reports);
        Assert.IsEmpty(publisher.Published);
    }
}
