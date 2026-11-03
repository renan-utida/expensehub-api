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
/// Tests of the isolation of the list, the detail and the Excel download by requester, for every kind of user: the five roles
/// alone, the two roles that may request together, and a user with no role. Reading needs no role, only ownership, so nobody
/// reads a request of someone else, not even an <c>Auditor</c> whose global privilege is over the data of its own report. A
/// request of someone else always gets the answer of one that does not exist.
/// </summary>
[TestClass]
public sealed class ReportIsolationTests
{
    private const string Reader = "reader-1";
    private const string Owner = "owner-2";

    /// <summary>The list of any user has no request of someone else, whatever the state of those requests.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Auditor")]
    [DataRow("Employee,Auditor")]
    [DataRow("Admin")]
    [DataRow("Approver")]
    [DataRow("Finance")]
    [DataRow("Admin,Approver,Finance")]
    [DataRow("")]
    public async Task ListAsync_NoUserSeesTheRequestsOfSomeoneElse(string roles)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        GivenReportsOf(repository, Owner);

        var list = await service.ListAsync(CallerWith(roles));

        Assert.IsEmpty(list);
    }

    /// <summary>When a user has requests of their own, the list has exactly those and nothing of the others, for every kind of user.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Auditor")]
    [DataRow("Employee,Auditor")]
    [DataRow("Admin")]
    [DataRow("")]
    public async Task ListAsync_HasExactlyTheOwnRequestsWhenOthersExist(string roles)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest own = ReportTestData.Report(Reader, ReportStatus.Pending, ReportScope.Mine);
        repository.Given(own);
        GivenReportsOf(repository, Owner);

        var list = await service.ListAsync(CallerWith(roles));

        Assert.AreEqual(own.Id, list.Single().Id);
    }

    /// <summary>The detail of a request of someone else is not given, in any state, to any kind of user, and looks like a request that does not exist.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Auditor")]
    [DataRow("Employee,Auditor")]
    [DataRow("Admin")]
    [DataRow("Approver")]
    [DataRow("Finance")]
    [DataRow("Admin,Approver,Finance")]
    [DataRow("")]
    public async Task GetAsync_NoUserReadsARequestOfSomeoneElse(string roles)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest[] others = GivenReportsOf(repository, Owner);
        ExpenseCaller caller = CallerWith(roles);

        Assert.IsNull(await service.GetAsync(caller, Guid.NewGuid()));

        foreach (ReportRequest other in others)
        {
            ReportRequest? found = await service.GetAsync(caller, other.Id);

            Assert.IsNull(found, $"{other.Status} {other.Scope} read by [{roles}]");
        }
    }

    /// <summary>The Excel of a request of someone else is not found, in any state, for any kind of user. It is never the conflict of an own request that is not completed, and it carries no data.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Auditor")]
    [DataRow("Employee,Auditor")]
    [DataRow("Admin")]
    [DataRow("Approver")]
    [DataRow("Finance")]
    [DataRow("Admin,Approver,Finance")]
    [DataRow("")]
    public async Task GetExcelAsync_NoUserDownloadsTheFileOfSomeoneElse(string roles)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest[] others = GivenReportsOf(repository, Owner);
        ExpenseCaller caller = CallerWith(roles);

        ReportExcelResult missing = await service.GetExcelAsync(caller, Guid.NewGuid());

        foreach (ReportRequest other in others)
        {
            ReportExcelResult result = await service.GetExcelAsync(caller, other.Id);

            Assert.AreEqual(ReportExcelStatus.NotFound, result.Status, $"{other.Status} {other.Scope} downloaded by [{roles}]");
            Assert.IsNull(result.FileName);
            Assert.AreEqual(0, result.Content.Length);
            Assert.AreEqual(missing.Status, result.Status);
        }
    }

    /// <summary>An Auditor who asked for the Global scope reads only the requests that Auditor made: the same report is found by the owner and not found by another Auditor.</summary>
    [TestMethod]
    public async Task GetAsync_GlobalReportOfOneAuditor_IsNotReadByAnotherAuditor()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest global = GlobalCompletedReport("auditor-1");
        repository.Given(global);

        ReportRequest? byOwner = await service.GetAsync(ReportTestData.Caller("auditor-1", "Auditor"), global.Id);
        ReportRequest? byAnotherAuditor = await service.GetAsync(ReportTestData.Caller("auditor-2", "Auditor"), global.Id);
        ReportExcelResult excelByOwner = await service.GetExcelAsync(ReportTestData.Caller("auditor-1", "Auditor"), global.Id);
        ReportExcelResult excelByAnotherAuditor = await service.GetExcelAsync(ReportTestData.Caller("auditor-2", "Auditor"), global.Id);

        Assert.AreSame(global, byOwner);
        Assert.IsNull(byAnotherAuditor);
        Assert.AreEqual(ReportExcelStatus.Ready, excelByOwner.Status);
        Assert.AreEqual(ReportExcelStatus.NotFound, excelByAnotherAuditor.Status);
    }

    /// <summary>Reading needs no role: a user with no role that may request a report still reads the requests that user owns.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Admin")]
    [DataRow("Approver")]
    [DataRow("Finance")]
    [DataRow("")]
    public async Task Reading_NeedsNoRoleOnlyOwnership(string roles)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest own = ReportTestData.CompletedReport(Reader);
        repository.Given(own);
        ExpenseCaller caller = CallerWith(roles);

        var list = await service.ListAsync(caller);
        ReportRequest? detail = await service.GetAsync(caller, own.Id);
        ReportExcelResult excel = await service.GetExcelAsync(caller, own.Id);

        Assert.AreEqual(own.Id, list.Single().Id);
        Assert.AreSame(own, detail);
        Assert.AreEqual(ReportExcelStatus.Ready, excel.Status);
    }

    /// <summary>An Auditor downloading a report of their own that is not completed gets the conflict, while the report of someone else gets not found: the 409 is only for the owner.</summary>
    [TestMethod]
    public async Task GetExcelAsync_Auditor_OwnNotCompletedIsAConflictAndSomeoneElsesIsNotFound()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest own = ReportTestData.Report("auditor-1", ReportStatus.Pending, ReportScope.Global);
        ReportRequest alien = ReportTestData.Report(Owner, ReportStatus.Pending, ReportScope.Global);
        repository.Given(own);
        repository.Given(alien);
        ExpenseCaller auditor = ReportTestData.Caller("auditor-1", "Auditor");

        ReportExcelResult ofOwn = await service.GetExcelAsync(auditor, own.Id);
        ReportExcelResult ofAlien = await service.GetExcelAsync(auditor, alien.Id);

        Assert.AreEqual(ReportExcelStatus.NotCompleted, ofOwn.Status);
        Assert.AreEqual(ReportExcelStatus.NotFound, ofAlien.Status);
    }

    /// <summary>Following the requests of others never reads an expense, so the data of others cannot leak through the reading routes.</summary>
    [TestMethod]
    public async Task Reading_NeverReadsAnExpense()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest[] others = GivenReportsOf(repository, Owner);
        ExpenseCaller auditor = ReportTestData.Caller(Reader, "Auditor");

        await service.ListAsync(auditor);

        foreach (ReportRequest other in others)
        {
            await service.GetAsync(auditor, other.Id);
            await service.GetExcelAsync(auditor, other.Id);
        }

        Assert.AreEqual(0, repository.ExpenseReads);
    }

    private static ExpenseCaller CallerWith(string roles)
    {
        return ReportTestData.Caller(Reader, roles.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static ReportRequest GlobalCompletedReport(string requesterId)
    {
        ReportQuery query = ReportTestData.Query(ReportScope.Global, requesterId);
        ReportRequest report = ReportTestData.CompletedReport(requesterId, ReportTestData.Calculate(ReportTestData.ExampleExpenses(), query));
        report.Scope = ReportScope.Global;

        return report;
    }

    private static ReportRequest[] GivenReportsOf(FakeReportRepository repository, string requesterId)
    {
        ReportRequest[] reports =
        [
            ReportTestData.Report(requesterId, ReportStatus.Pending, ReportScope.Mine),
            ReportTestData.Report(requesterId, ReportStatus.Processing, ReportScope.Global),
            GlobalCompletedReport(requesterId),
            ReportTestData.FailedReport(requesterId),
        ];

        foreach (ReportRequest report in reports)
        {
            repository.Given(report);
        }

        return reports;
    }

    private static (ReportService Service, FakeReportRepository Repository) Create()
    {
        var repository = new FakeReportRepository();
        var service = new ReportService(repository, new FakeReportPublisher(repository), new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository);
    }
}
