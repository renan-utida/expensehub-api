using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Hand-written fake of <see cref="ReportService"/> for the controller tests. It runs none of the rules of the service: every
/// method records what the controller passed and answers with the outcome the test chose, so a controller test checks only
/// what belongs to the controller. The rules of the service have their own tests.
/// </summary>
internal sealed class FakeReportService : ReportService
{
    private readonly List<string> _calls = new();

    /// <summary>Initializes a new instance of the <see cref="FakeReportService"/> class, which never reaches its repository.</summary>
    public FakeReportService()
        : base(
            new FakeReportRepository(),
            new FakeReportPublisher(),
            new FixedTimeProvider(ExpenseTestData.Now),
            NullLogger<ReportService>.Instance)
    {
    }

    /// <summary>Gets or sets the outcome of requesting a report.</summary>
    public ReportOperationResult RequestResult { get; set; } = ReportOperationResult.StorageUnavailable();

    /// <summary>Gets or sets the requests the list answers with.</summary>
    public IReadOnlyList<ReportRequest> ListResult { get; set; } = Array.Empty<ReportRequest>();

    /// <summary>Gets or sets the request the detail answers with, or <c>null</c> for one that cannot be read.</summary>
    public ReportRequest? GetResult { get; set; }

    /// <summary>Gets or sets the outcome of the download.</summary>
    public ReportExcelResult ExcelResult { get; set; } = ReportExcelResult.NotFound();

    /// <summary>Gets the names of the methods the controller called, in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Gets the user the controller passed in the last call.</summary>
    public ExpenseCaller? LastCaller { get; private set; }

    /// <summary>Gets the report identifier the controller passed in the last call.</summary>
    public Guid? LastReportId { get; private set; }

    /// <summary>Gets the fields the controller passed in the last request.</summary>
    public ReportRequestDetails? LastDetails { get; private set; }

    /// <inheritdoc />
    public override Task<ReportOperationResult> RequestAsync(ExpenseCaller caller, ReportRequestDetails details)
    {
        Record(nameof(RequestAsync), caller, null, details);

        return Task.FromResult(RequestResult);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<ReportRequest>> ListAsync(ExpenseCaller caller)
    {
        Record(nameof(ListAsync), caller, null, null);

        return Task.FromResult(ListResult);
    }

    /// <inheritdoc />
    public override Task<ReportRequest?> GetAsync(ExpenseCaller caller, Guid reportId)
    {
        Record(nameof(GetAsync), caller, reportId, null);

        return Task.FromResult(GetResult);
    }

    /// <inheritdoc />
    public override Task<ReportExcelResult> GetExcelAsync(ExpenseCaller caller, Guid reportId)
    {
        Record(nameof(GetExcelAsync), caller, reportId, null);

        return Task.FromResult(ExcelResult);
    }

    private void Record(string method, ExpenseCaller caller, Guid? reportId, ReportRequestDetails? details)
    {
        _calls.Add(method);
        LastCaller = caller;
        LastReportId = reportId;
        LastDetails = details;
    }
}
