using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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
/// Tests of how the requester and the scope reach the processing: the report query is built only from the request that was
/// saved when the request was accepted, so the worker never reads the roles of the user again and never trusts the content of
/// the message, which carries only the identifier. A <c>Mine</c> report never adds up the expenses of another employee.
/// </summary>
[TestClass]
public sealed class ReportQueryFromRequestTests
{
    /// <summary>The query has the requester, the scope and the period of the saved request, for both scopes.</summary>
    /// <param name="scope">The scope of the saved request.</param>
    [TestMethod]
    [DataRow(ReportScope.Mine)]
    [DataRow(ReportScope.Global)]
    public void From_CopiesTheRequesterTheScopeAndThePeriodOfTheSavedRequest(ReportScope scope)
    {
        ReportRequest saved = ReportTestData.Report("requester-9", ReportStatus.Pending, scope);

        ReportQuery query = ReportQuery.From(saved);

        Assert.AreEqual(scope, query.Scope);
        Assert.AreEqual("requester-9", query.RequesterId);
        Assert.AreEqual(saved.FromDate, query.FromDate);
        Assert.AreEqual(saved.ToDate, query.ToDate);
    }

    /// <summary>A missing request is rejected, so a query is never built from nothing.</summary>
    [TestMethod]
    public void From_MissingRequest_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportQuery.From(null!));
    }

    /// <summary>A saved request without a requester cannot become a query, so the scope Mine can never fall back to everyone.</summary>
    [TestMethod]
    public void From_RequestWithoutRequester_IsRejected()
    {
        ReportRequest saved = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Pending, ReportScope.Mine);
        saved.RequesterId = string.Empty;

        Assert.Throws<ArgumentException>(() => ReportQuery.From(saved));
    }

    /// <summary>From request to result: an Employee asks for Mine, and the saved request makes the calculation add up only the expenses of that Employee.</summary>
    [TestMethod]
    public async Task EmployeeMine_TheSavedRequestAddsUpOnlyTheExpensesOfTheRequester()
    {
        (ReportService service, FakeReportRepository repository) = Create();

        ReportSummary summary = await RequestAndCalculateAsync(service, repository, ReportTestData.Caller(ReportTestData.Requester, "Employee"), "Mine");

        Assert.AreEqual(3, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("175.00"), summary.TotalAmount);
    }

    /// <summary>Another Employee asking for Mine over the same expenses gets only their own expenses, never those of the first one.</summary>
    [TestMethod]
    public async Task AnotherEmployeeMine_GetsOnlyTheirOwnExpenses()
    {
        (ReportService service, FakeReportRepository repository) = Create();

        ReportSummary summary = await RequestAndCalculateAsync(service, repository, ReportTestData.Caller(ReportTestData.Other, "Employee"), "Mine");

        Assert.AreEqual(1, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("200.00"), summary.TotalAmount);
    }

    /// <summary>An Auditor asks for Global, and the saved request makes the calculation add up the expenses of everyone in the period.</summary>
    [TestMethod]
    public async Task AuditorGlobal_TheSavedRequestAddsUpTheExpensesOfEveryone()
    {
        (ReportService service, FakeReportRepository repository) = Create();

        ReportSummary summary = await RequestAndCalculateAsync(service, repository, ReportTestData.Caller("auditor-1", "Auditor"), "Global");

        Assert.AreEqual(4, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("375.00"), summary.TotalAmount);
    }

    /// <summary>A user with both roles gets the scope that was asked for in each request, so the two requests of the same user are calculated differently.</summary>
    [TestMethod]
    public async Task EmployeeAndAuditor_EachSavedRequestKeepsItsOwnScope()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ExpenseCaller both = ReportTestData.Caller(ReportTestData.Requester, "Employee", "Auditor");

        ReportSummary mine = await RequestAndCalculateAsync(service, repository, both, "Mine");
        ReportSummary global = await RequestAndCalculateAsync(service, repository, both, "Global");

        Assert.AreEqual(3, mine.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("175.00"), mine.TotalAmount);
        Assert.AreEqual(4, global.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("375.00"), global.TotalAmount);
        CollectionAssert.AreEqual(new[] { ReportScope.Mine, ReportScope.Global }, repository.Reports.Select(report => report.Scope).ToArray());
    }

    /// <summary>The query is built from the saved request alone: the method takes nothing else, so no role of the user and no content of a message can change it.</summary>
    [TestMethod]
    public void From_TakesNothingButTheSavedRequest()
    {
        MethodInfo from = typeof(ReportQuery).GetMethod(nameof(ReportQuery.From))!;

        ParameterInfo parameter = from.GetParameters().Single();

        Assert.AreEqual(typeof(ReportRequest), parameter.ParameterType);
    }

    /// <summary>The message to the worker carries only the identifier of the report: the requester, the scope and the period are never in it.</summary>
    [TestMethod]
    public void Publisher_MessageCarriesOnlyTheReportIdentifier()
    {
        MethodInfo publish = typeof(IReportPublisher).GetMethod(nameof(IReportPublisher.PublishAsync))!;

        Type[] parameters = publish.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        CollectionAssert.AreEqual(new[] { typeof(Guid), typeof(CancellationToken) }, parameters);
        Assert.HasCount(1, typeof(IReportPublisher).GetMethods());
    }

    private static async Task<ReportSummary> RequestAndCalculateAsync(ReportService service, FakeReportRepository repository, ExpenseCaller caller, string scope)
    {
        repository.Expenses.Clear();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());

        ReportOperationResult result = await service.RequestAsync(caller, ReportTestData.Details(scope));

        Assert.AreEqual(ReportOperationStatus.Accepted, result.Status);
        ReportQuery query = ReportQuery.From(result.Report!);
        IReadOnlyList<ReportExpenseLine> lines = await repository.ListExpenseLinesAsync(query);

        return ReportCalculator.Summarize(lines, ReportTestData.Now);
    }

    private static (ReportService Service, FakeReportRepository Repository) Create()
    {
        var repository = new FakeReportRepository();
        var service = new ReportService(repository, new FakeReportPublisher(repository), new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository);
    }
}
