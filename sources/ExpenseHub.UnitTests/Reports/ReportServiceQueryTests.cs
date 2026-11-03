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
/// Tests of following a report: the list and the detail only show the requests of the caller, a request of someone else looks
/// like one that does not exist, and reading returns the result that was saved without reading any expense again.
/// </summary>
[TestClass]
public sealed class ReportServiceQueryTests
{
    /// <summary>The list has only the requests of the caller, newest first.</summary>
    [TestMethod]
    public async Task ListAsync_ReturnsOnlyTheRequestsOfTheCallerNewestFirst()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest older = ReportTestData.Report(ReportTestData.Requester);
        older.CreatedAtUtc = ReportTestData.Now.AddHours(-2);
        ReportRequest newer = ReportTestData.Report(ReportTestData.Requester);
        newer.CreatedAtUtc = ReportTestData.Now.AddHours(-1);
        repository.Given(older);
        repository.Given(ReportTestData.Report(ReportTestData.Other));
        repository.Given(newer);

        var list = await service.ListAsync(ReportTestData.Caller(ReportTestData.Requester));

        CollectionAssert.AreEqual(new[] { newer.Id, older.Id }, list.Select(report => report.Id).ToArray());
    }

    /// <summary>A caller with no request gets an empty list, never the requests of others.</summary>
    [TestMethod]
    public async Task ListAsync_CallerWithoutRequests_GetsAnEmptyList()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        repository.Given(ReportTestData.Report(ReportTestData.Other));

        var list = await service.ListAsync(ReportTestData.Caller(ReportTestData.Requester));

        Assert.IsEmpty(list);
    }

    /// <summary>Even a caller with the role Auditor, who may ask for the Global scope, lists only the requests he or she made.</summary>
    [TestMethod]
    public async Task ListAsync_AuditorSeesOnlyTheirOwnRequests()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest own = ReportTestData.Report("auditor-1", scope: ReportScope.Global);
        repository.Given(own);
        repository.Given(ReportTestData.Report(ReportTestData.Requester));

        var list = await service.ListAsync(ReportTestData.Caller("auditor-1", "Auditor"));

        Assert.AreEqual(own.Id, list.Single().Id);
    }

    /// <summary>The detail of the caller's own request is returned.</summary>
    [TestMethod]
    public async Task GetAsync_OwnRequest_IsReturned()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest own = ReportTestData.Report(ReportTestData.Requester);
        repository.Given(own);

        ReportRequest? found = await service.GetAsync(ReportTestData.Caller(ReportTestData.Requester), own.Id);

        Assert.AreSame(own, found);
    }

    /// <summary>The request of someone else and one that does not exist give the same answer, so the detail and the result of others are never exposed.</summary>
    [TestMethod]
    public async Task GetAsync_RequestOfAnotherUserOrMissing_GivesTheSameAnswer()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest alien = ReportTestData.CompletedReport(ReportTestData.Other);
        repository.Given(alien);
        ExpenseCaller caller = ReportTestData.Caller(ReportTestData.Requester);

        ReportRequest? ofAnother = await service.GetAsync(caller, alien.Id);
        ReportRequest? missing = await service.GetAsync(caller, Guid.NewGuid());

        Assert.IsNull(ofAnother);
        Assert.IsNull(missing);
    }

    /// <summary>The Auditor does not read the request of another user either, even if it asked for the Global scope.</summary>
    [TestMethod]
    public async Task GetAsync_AuditorDoesNotReadTheRequestOfAnotherUser()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest alien = ReportTestData.Report(ReportTestData.Requester);
        repository.Given(alien);

        ReportRequest? found = await service.GetAsync(ReportTestData.Caller("auditor-1", "Auditor"), alien.Id);

        Assert.IsNull(found);
    }

    /// <summary>After an expense changes, the detail returns the result that was saved, and no expense is read to answer.</summary>
    [TestMethod]
    public async Task GetAsync_AfterAnExpenseChanges_ReturnsTheSavedResultWithoutRecalculating()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());
        ReportSummary saved = ReportTestData.Calculate(repository.Expenses, ReportTestData.Query(ReportScope.Mine));
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester, saved);
        repository.Given(report);

        // Everything changes after the report was completed: a state, an amount, and a new expense appears.
        repository.Expenses[0].Status = ExpenseStatus.Rejected;
        repository.Expenses[1].Amount = 9999.99m;
        repository.Expenses.Add(ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "1.00", ReportTestData.From));

        ReportRequest? read = await service.GetAsync(ReportTestData.Caller(ReportTestData.Requester), report.Id);
        ReportResponse envelope = ReportResponse.From(read!);

        Assert.AreEqual(3, envelope.Result!.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("175.00"), envelope.Result.TotalAmount);
        Assert.AreEqual(0, repository.ExpenseReads);
    }

    /// <summary>Listing does not read any expense either.</summary>
    [TestMethod]
    public async Task ListAsync_DoesNotReadAnyExpense()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        repository.Given(ReportTestData.CompletedReport(ReportTestData.Requester));

        await service.ListAsync(ReportTestData.Caller(ReportTestData.Requester));

        Assert.AreEqual(0, repository.ExpenseReads);
    }

    /// <summary>The service refuses a caller without an identifier.</summary>
    /// <param name="userId">An identifier that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task ListAndGet_CallerWithoutIdentifier_Throw(string? userId)
    {
        (ReportService service, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(ReportTestData.Caller(userId!)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(ReportTestData.Caller(userId!), Guid.NewGuid()));
    }

    private static (ReportService Service, FakeReportRepository Repository) Create()
    {
        var repository = new FakeReportRepository();
        var service = new ReportService(repository, new FakeReportPublisher(repository), new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository);
    }
}
