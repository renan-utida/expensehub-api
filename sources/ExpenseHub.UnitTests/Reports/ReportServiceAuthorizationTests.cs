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
/// Tests of the authorization of requesting a report in the service: which role may ask for which scope, the order of the
/// answers (the role first, then the validation of the data, then the scope), that a refused request saves nothing and
/// publishes nothing, and that the requester and the scope are recorded on the saved request for the worker to use.
/// </summary>
[TestClass]
public sealed class ReportServiceAuthorizationTests
{
    /// <summary>A user with no role that may request a report is refused for either scope, and nothing is saved or published.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    /// <param name="scope">The scope asked for.</param>
    [TestMethod]
    [DataRow("", "Mine")]
    [DataRow("", "Global")]
    [DataRow("Admin", "Mine")]
    [DataRow("Admin", "Global")]
    [DataRow("Approver", "Mine")]
    [DataRow("Approver", "Global")]
    [DataRow("Finance", "Mine")]
    [DataRow("Finance", "Global")]
    [DataRow("Admin,Approver,Finance", "Mine")]
    [DataRow("Admin,Approver,Finance", "Global")]
    public async Task RequestAsync_NoRoleThatMayRequest_IsForbiddenAndSavesNothing(string roles, string scope)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();

        ReportOperationResult result = await service.RequestAsync(CallerWith(roles), ReportTestData.Details(scope));

        AssertForbiddenAndNothingHappened(result, repository, publisher);
    }

    /// <summary>The role comes before the validation: a user with no role that may request is refused even when the data is invalid.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("Admin")]
    [DataRow("Approver")]
    [DataRow("Finance")]
    public async Task RequestAsync_NoRoleThatMayRequest_IsForbiddenBeforeTheDataIsValidated(string roles)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        var invalidPeriod = new ReportRequestDetails(ReportTestData.To, ReportTestData.From, "Global");
        var nothingAtAll = new ReportRequestDetails(null, null, null);

        ReportOperationResult first = await service.RequestAsync(CallerWith(roles), invalidPeriod);
        ReportOperationResult second = await service.RequestAsync(CallerWith(roles), nothingAtAll);

        AssertForbiddenAndNothingHappened(first, repository, publisher);
        AssertForbiddenAndNothingHappened(second, repository, publisher);
    }

    /// <summary>The scope is allowed only to the matching role: Employee asks for Mine, Auditor asks for Global, and both roles ask for both.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    /// <param name="scope">The scope asked for.</param>
    /// <param name="allowed">Whether the request is accepted.</param>
    [TestMethod]
    [DataRow("Employee", "Mine", true)]
    [DataRow("Employee", "Global", false)]
    [DataRow("Auditor", "Mine", false)]
    [DataRow("Auditor", "Global", true)]
    [DataRow("Employee,Auditor", "Mine", true)]
    [DataRow("Employee,Auditor", "Global", true)]
    [DataRow("Admin,Employee", "Mine", true)]
    [DataRow("Admin,Employee", "Global", false)]
    [DataRow("Approver,Auditor", "Mine", false)]
    [DataRow("Approver,Auditor", "Global", true)]
    [DataRow("Employee,Approver,Finance", "Global", false)]
    public async Task RequestAsync_ScopeFollowsTheRoles(string roles, string scope, bool allowed)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();

        ReportOperationResult result = await service.RequestAsync(CallerWith(roles), ReportTestData.Details(scope));

        if (allowed)
        {
            Assert.AreEqual(ReportOperationStatus.Accepted, result.Status);
            Assert.HasCount(1, repository.Reports);
            Assert.HasCount(1, publisher.Published);
        }
        else
        {
            AssertForbiddenAndNothingHappened(result, repository, publisher);
        }
    }

    /// <summary>An Employee asking for Global is denied without creating anything that a worker could process.</summary>
    [TestMethod]
    public async Task RequestAsync_EmployeeAsksForGlobal_CreatesNoRequestAndNoMessage()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller("employee-1", "Employee"), ReportTestData.Details("Global"));

        AssertForbiddenAndNothingHappened(result, repository, publisher);
        Assert.AreEqual(0, repository.ExpenseReads);
    }

    /// <summary>The validation comes before the scope: invalid data from a user who has an allowed role is a validation failure, whatever the scope.</summary>
    [TestMethod]
    public async Task RequestAsync_InvalidDataWithAnAllowedRole_IsValidationFailedBeforeTheScopeIsChecked()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        ExpenseCaller employee = ReportTestData.Caller("employee-1", "Employee");
        ExpenseCaller auditor = ReportTestData.Caller("auditor-1", "Auditor");

        ReportOperationResult employeeInvertedGlobal = await service.RequestAsync(employee, new ReportRequestDetails(ReportTestData.To, ReportTestData.From, "Global"));
        ReportOperationResult auditorInvertedMine = await service.RequestAsync(auditor, new ReportRequestDetails(ReportTestData.To, ReportTestData.From, "Mine"));
        ReportOperationResult employeeMissingDatesGlobal = await service.RequestAsync(employee, new ReportRequestDetails(null, null, "Global"));
        ReportOperationResult employeeUnknownScope = await service.RequestAsync(employee, ReportTestData.Details("Everyone"));

        Assert.AreEqual(ReportOperationStatus.ValidationFailed, employeeInvertedGlobal.Status);
        Assert.AreEqual(ReportOperationStatus.ValidationFailed, auditorInvertedMine.Status);
        Assert.AreEqual(ReportOperationStatus.ValidationFailed, employeeMissingDatesGlobal.Status);
        Assert.AreEqual(ReportOperationStatus.ValidationFailed, employeeUnknownScope.Status);
        Assert.AreEqual(0, repository.AddCalls);
        Assert.IsEmpty(publisher.Published);
    }

    /// <summary>The scope is refused only after the data is valid: the same Employee with the same data but a valid period gets the refusal.</summary>
    [TestMethod]
    public async Task RequestAsync_SameEmployeeAndScope_Is400WithBadDataAnd403WithGoodData()
    {
        (ReportService service, _, _) = Create();
        ExpenseCaller employee = ReportTestData.Caller("employee-1", "Employee");

        ReportOperationResult bad = await service.RequestAsync(employee, new ReportRequestDetails(ReportTestData.To, ReportTestData.From, "Global"));
        ReportOperationResult good = await service.RequestAsync(employee, ReportTestData.Details("Global"));

        Assert.AreEqual(ReportOperationStatus.ValidationFailed, bad.Status);
        Assert.AreEqual(ReportOperationStatus.Forbidden, good.Status);
    }

    /// <summary>An accepted request records the requester and the scope on the saved request, which is what the worker will read.</summary>
    /// <param name="roles">The roles of the user.</param>
    /// <param name="scopeName">The name of the scope.</param>
    /// <param name="expected">The scope that must be recorded.</param>
    [TestMethod]
    [DataRow("Employee", "Mine", ReportScope.Mine)]
    [DataRow("Auditor", "Global", ReportScope.Global)]
    [DataRow("Employee,Auditor", "Mine", ReportScope.Mine)]
    [DataRow("Employee,Auditor", "Global", ReportScope.Global)]
    public async Task RequestAsync_Accepted_RecordsTheRequesterAndTheScopeOnTheSavedRequest(string roles, string scopeName, ReportScope expected)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        ExpenseCaller caller = ReportTestData.Caller("user-from-the-token", roles.Split(','));

        ReportOperationResult result = await service.RequestAsync(caller, ReportTestData.Details(scopeName));

        Assert.AreEqual(ReportOperationStatus.Accepted, result.Status);
        ReportRequest saved = repository.Reports.Single();
        Assert.AreEqual("user-from-the-token", saved.RequesterId);
        Assert.AreEqual(expected, saved.Scope);
        Assert.AreEqual(ReportStatus.Pending, saved.Status);
        CollectionAssert.AreEqual(new[] { saved.Id }, publisher.Published.ToArray());
    }

    /// <summary>A refusal has no report, no identifier and no validation errors, so the endpoint cannot make up an answer from it.</summary>
    [TestMethod]
    public async Task RequestAsync_Forbidden_HasNoReportAndNoErrors()
    {
        (ReportService service, _, _) = Create();

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller("employee-1", "Employee"), ReportTestData.Details("Global"));

        Assert.AreEqual(ReportOperationStatus.Forbidden, result.Status);
        Assert.IsNull(result.Report);
        Assert.IsEmpty(result.Errors);
    }

    /// <summary>The refusal comes before the broker: a publisher that would not confirm changes nothing for a user who may not ask, who still gets the refusal and no identifier.</summary>
    [TestMethod]
    public async Task RequestAsync_ForbiddenScope_IsNeverAServiceUnavailableAnswer()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        publisher.Result = ReportPublishResult.NotConfirmed;
        publisher.ThrowOnPublish = true;
        repository.FailOnAdd = true;

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller("employee-1", "Employee"), ReportTestData.Details("Global"));

        Assert.AreEqual(ReportOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(0, repository.AddCalls);
    }

    /// <summary>For a user who may ask, a broker that does not confirm still gives the saved identifier, so the authorization does not change the 503 contract.</summary>
    [TestMethod]
    public async Task RequestAsync_AllowedScopeAndBrokerDoesNotConfirm_StillKeepsTheSavedReportPending()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = Create();
        publisher.Result = ReportPublishResult.NotConfirmed;

        ReportOperationResult result = await service.RequestAsync(ReportTestData.Caller("auditor-1", "Auditor"), ReportTestData.Details("Global"));

        Assert.AreEqual(ReportOperationStatus.NotPublished, result.Status);
        Assert.AreEqual(repository.Reports.Single().Id, result.Report!.Id);
        Assert.AreEqual(ReportStatus.Pending, repository.Reports.Single().Status);
    }

    private static ExpenseCaller CallerWith(string roles)
    {
        return ReportTestData.Caller("user-1", roles.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static (ReportService Service, FakeReportRepository Repository, FakeReportPublisher Publisher) Create()
    {
        var repository = new FakeReportRepository();
        var publisher = new FakeReportPublisher(repository);
        var service = new ReportService(repository, publisher, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository, publisher);
    }

    private static void AssertForbiddenAndNothingHappened(ReportOperationResult result, FakeReportRepository repository, FakeReportPublisher publisher)
    {
        Assert.AreEqual(ReportOperationStatus.Forbidden, result.Status);
        Assert.IsNull(result.Report);
        Assert.AreEqual(0, repository.AddCalls);
        Assert.IsEmpty(repository.Reports);
        Assert.IsEmpty(publisher.Published);
    }
}
