using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of downloading the Excel file: only the caller's own completed report gives a file, an own report that is not
/// completed is a conflict, one of someone else or a missing one is not found, and the file is built from the saved result
/// without reading any expense.
/// </summary>
[TestClass]
public sealed class ReportServiceExcelTests
{
    /// <summary>The own completed report gives a file named report-{id}.xlsx with the content of an xlsx.</summary>
    [TestMethod]
    public async Task GetExcelAsync_OwnCompletedReport_GivesTheFile()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        repository.Given(report);

        ReportExcelResult result = await service.GetExcelAsync(ReportTestData.Caller(ReportTestData.Requester), report.Id);

        Assert.AreEqual(ReportExcelStatus.Ready, result.Status);
        Assert.AreEqual($"report-{report.Id}.xlsx", result.FileName);
        Assert.IsGreaterThan(0, result.Content.Length);
        Assert.AreEqual((byte)'P', result.Content.Span[0]);
        Assert.AreEqual((byte)'K', result.Content.Span[1]);

        // The file is not only shaped like a package: it opens, names this report and ends with the saved grand total.
        string[] cells = CellTexts(result.Content);
        CollectionAssert.Contains(cells, report.Id.ToString());
        Assert.AreEqual("175.00", cells[^1]);
    }

    /// <summary>An own report that is pending, processing or failed is a conflict, and no file or data is given.</summary>
    /// <param name="status">The state of the report.</param>
    [TestMethod]
    [DataRow(ReportStatus.Pending)]
    [DataRow(ReportStatus.Processing)]
    [DataRow(ReportStatus.Failed)]
    public async Task GetExcelAsync_OwnReportNotCompleted_IsAConflict(ReportStatus status)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest report = status == ReportStatus.Failed
            ? ReportTestData.FailedReport(ReportTestData.Requester)
            : ReportTestData.Report(ReportTestData.Requester, status);
        repository.Given(report);

        ReportExcelResult result = await service.GetExcelAsync(ReportTestData.Caller(ReportTestData.Requester), report.Id);

        Assert.AreEqual(ReportExcelStatus.NotCompleted, result.Status);
        Assert.IsNull(result.FileName);
        Assert.AreEqual(0, result.Content.Length);
    }

    /// <summary>The state decides, not the presence of a result: an own report that is not completed gets no file even if a result happens to be saved with it. The table only demands a result for a completed report; it does not forbid one on the others.</summary>
    /// <param name="status">The state of the report.</param>
    [TestMethod]
    [DataRow(ReportStatus.Pending)]
    [DataRow(ReportStatus.Processing)]
    [DataRow(ReportStatus.Failed)]
    public async Task GetExcelAsync_OwnReportNotCompleted_GivesNoFileEvenWithAResultSaved(ReportStatus status)
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest report = status == ReportStatus.Failed
            ? ReportTestData.FailedReport(ReportTestData.Requester)
            : ReportTestData.Report(ReportTestData.Requester, status);
        report.ResultJson = ReportResultSerializer.Serialize(ReportTestData.MineSummary());
        repository.Given(report);

        ReportExcelResult result = await service.GetExcelAsync(ReportTestData.Caller(ReportTestData.Requester), report.Id);

        Assert.AreEqual(ReportExcelStatus.NotCompleted, result.Status);
        Assert.IsNull(result.FileName);
        Assert.AreEqual(0, result.Content.Length);
    }

    /// <summary>The completed report of another user is not found, and no file or data is given.</summary>
    [TestMethod]
    public async Task GetExcelAsync_ReportOfAnotherUser_IsNotFound()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest alien = ReportTestData.CompletedReport(ReportTestData.Other);
        repository.Given(alien);

        ReportExcelResult result = await service.GetExcelAsync(ReportTestData.Caller(ReportTestData.Requester), alien.Id);

        Assert.AreEqual(ReportExcelStatus.NotFound, result.Status);
        Assert.IsNull(result.FileName);
        Assert.AreEqual(0, result.Content.Length);
    }

    /// <summary>The Auditor does not download the file of another user's report either.</summary>
    [TestMethod]
    public async Task GetExcelAsync_AuditorForTheReportOfAnotherUser_IsNotFound()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        ReportRequest alien = ReportTestData.CompletedReport(ReportTestData.Requester);
        repository.Given(alien);

        ReportExcelResult result = await service.GetExcelAsync(ReportTestData.Caller("auditor-1", "Auditor"), alien.Id);

        Assert.AreEqual(ReportExcelStatus.NotFound, result.Status);
        Assert.IsNull(result.FileName);
        Assert.AreEqual(0, result.Content.Length);
    }

    /// <summary>A report that does not exist is not found, the same answer as for one of another user.</summary>
    [TestMethod]
    public async Task GetExcelAsync_MissingReport_IsNotFound()
    {
        (ReportService service, _) = Create();

        ReportExcelResult result = await service.GetExcelAsync(ReportTestData.Caller(ReportTestData.Requester), Guid.NewGuid());

        Assert.AreEqual(ReportExcelStatus.NotFound, result.Status);
        Assert.IsNull(result.FileName);
        Assert.AreEqual(0, result.Content.Length);
    }

    /// <summary>The file is built from the saved result: expenses that change afterwards are not read and do not change the file.</summary>
    [TestMethod]
    public async Task GetExcelAsync_AfterAnExpenseChanges_StillHasTheSavedResultAndReadsNoExpense()
    {
        (ReportService service, FakeReportRepository repository) = Create();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        repository.Given(report);
        ExpenseCaller caller = ReportTestData.Caller(ReportTestData.Requester);
        ReportExcelResult before = await service.GetExcelAsync(caller, report.Id);

        repository.Expenses[0].Status = ExpenseStatus.Rejected;
        repository.Expenses[1].Amount = 9999.99m;
        ReportExcelResult after = await service.GetExcelAsync(caller, report.Id);

        // The package stores the time of each entry, so two files are compared by what their cells hold, not by their bytes.
        CollectionAssert.AreEqual(CellTexts(before.Content), CellTexts(after.Content));
        Assert.AreEqual("175.00", CellTexts(after.Content)[^1]);
        Assert.AreEqual(0, repository.ExpenseReads);
    }

    /// <summary>The service refuses a caller without an identifier.</summary>
    /// <param name="userId">An identifier that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task GetExcelAsync_CallerWithoutIdentifier_Throws(string? userId)
    {
        (ReportService service, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetExcelAsync(ReportTestData.Caller(userId!), Guid.NewGuid()));
    }

    private static string[] CellTexts(ReadOnlyMemory<byte> content)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(new MemoryStream(content.ToArray()), false);
        WorksheetPart sheet = document.WorkbookPart!.WorksheetParts.Single();

        return sheet.Worksheet!.Descendants<Cell>()
            .Select(cell => cell.InlineString?.Text?.Text ?? cell.CellValue?.Text ?? string.Empty)
            .ToArray();
    }

    private static (ReportService Service, FakeReportRepository Repository) Create()
    {
        var repository = new FakeReportRepository();
        var service = new ReportService(repository, new FakeReportPublisher(repository), new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository);
    }
}
