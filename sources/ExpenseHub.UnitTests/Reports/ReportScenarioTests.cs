using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExpenseHub.Api.Controllers;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// The scenarios of the minimum table of the assignment, run through the real <see cref="ReportsController"/>, the real
/// <see cref="ReportService"/> and the real <see cref="ReportMessageProcessor"/> together, on the hand-written fakes of the
/// repository, of the publisher and of the state of the worker. Every test says what the client receives and what was left
/// saved, not which method was called. The other test classes check each part alone; these check that the parts, put together,
/// give the answers of the contract. Nothing here touches a database, a network, RabbitMQ or Docker.
/// </summary>
[TestClass]
public sealed class ReportScenarioTests
{
    private static readonly JsonSerializerOptions _webJson = new(JsonSerializerDefaults.Web);

    /// <summary>Scenario 3: an inverted period, missing dates or a scope that does not exist is answered with 400 on the right field, and nothing is saved or published.</summary>
    /// <param name="from">The start date as text, or <c>null</c> for none.</param>
    /// <param name="to">The end date as text, or <c>null</c> for none.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="field">The field the error must be reported on.</param>
    [TestMethod]
    [DataRow("2026-09-30", "2026-09-01", "Mine", "FromDate")]
    [DataRow(null, null, "Mine", "FromDate")]
    [DataRow("2026-09-01", "2026-09-30", "Everyone", "Scope")]
    public async Task Scenario3_RejectedRequest_Is400AndNothingIsSavedOrPublished(string? from, string? to, string scope, string field)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = CreateWeb();
        var body = new CreateReportRequest { FromDate = ParseDate(from), ToDate = ParseDate(to), Scope = scope };

        IActionResult result = await ControllerFor(service, ReportTestData.Requester, "Employee").RequestAsync(body);

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        ValidationProblemDetails problem = Assert.IsInstanceOfType<ValidationProblemDetails>(objectResult.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        Assert.IsTrue(problem.Errors.ContainsKey(field), $"The error must be on {field}.");
        Assert.IsEmpty(repository.Reports);
        Assert.AreEqual(0, repository.AddCalls);
        Assert.IsEmpty(publisher.Published);
    }

    /// <summary>Scenario 4: an Employee who asks for Global is answered with 403, and no request, identifier or message is created for a worker to process.</summary>
    [TestMethod]
    public async Task Scenario4_EmployeeAsksForGlobal_Is403AndNothingIsCreated()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = CreateWeb();

        IActionResult result = await ControllerFor(service, ReportTestData.Requester, "Employee").RequestAsync(ValidBody("Global"));

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
        Assert.IsFalse(problem.Extensions.ContainsKey("id"));
        Assert.IsEmpty(repository.Reports);
        Assert.AreEqual(0, repository.AddCalls);
        Assert.IsEmpty(publisher.Published);
    }

    /// <summary>Scenario 5: the detail of a report of another user is a 404 that has the same body as a report that does not exist, carries no data of the report and is not in the list of the caller, even for someone who is Employee and Auditor.</summary>
    [TestMethod]
    public async Task Scenario5_DetailOfAnotherUsersReport_Is404WithTheBodyOfAMissingReport()
    {
        (ReportService service, FakeReportRepository repository, _) = CreateWeb();
        ReportRequest alien = ReportTestData.CompletedReport(ReportTestData.Other);
        repository.Given(alien);
        ReportsController controller = ControllerFor(service, ReportTestData.Requester, "Employee", "Auditor");

        ActionResult<ReportResponse> ofAlien = await controller.GetAsync(alien.Id);
        ActionResult<ReportResponse> ofMissing = await controller.GetAsync(Guid.NewGuid());
        ActionResult<IReadOnlyList<ReportResponse>> list = await controller.ListAsync();

        Assert.IsNull(ofAlien.Value);
        ProblemDetails alienProblem = NotFoundProblem(ofAlien.Result);
        AssertSameProblem(NotFoundProblem(ofMissing.Result), alienProblem);
        AssertCarriesNoDataOf(alien, alienProblem);
        Assert.IsEmpty(list.Value!);
    }

    /// <summary>Scenario 5: the Excel of a report of another user is a 404 with no file, with the same body as a report that does not exist and no data of the report.</summary>
    [TestMethod]
    public async Task Scenario5_ExcelOfAnotherUsersReport_Is404WithoutAFile()
    {
        (ReportService service, FakeReportRepository repository, _) = CreateWeb();
        ReportRequest alien = ReportTestData.CompletedReport(ReportTestData.Other);
        repository.Given(alien);
        ReportsController controller = ControllerFor(service, ReportTestData.Requester, "Employee", "Auditor");

        IActionResult ofAlien = await controller.ExcelAsync(alien.Id);
        IActionResult ofMissing = await controller.ExcelAsync(Guid.NewGuid());

        Assert.IsNotInstanceOfType<FileResult>(ofAlien);
        ProblemDetails alienProblem = NotFoundProblem(ofAlien);
        AssertSameProblem(NotFoundProblem(ofMissing), alienProblem);
        AssertCarriesNoDataOf(alien, alienProblem);
    }

    /// <summary>Scenario 9: the Excel of an own report that is pending, processing or failed is a 409 with no file and nothing of a result.</summary>
    /// <param name="status">The state of the own report.</param>
    [TestMethod]
    [DataRow(ReportStatus.Pending)]
    [DataRow(ReportStatus.Processing)]
    [DataRow(ReportStatus.Failed)]
    public async Task Scenario9_OwnReportNotCompleted_Is409WithoutAFile(ReportStatus status)
    {
        (ReportService service, FakeReportRepository repository, _) = CreateWeb();
        ReportRequest own = status == ReportStatus.Failed
            ? ReportTestData.FailedReport(ReportTestData.Requester)
            : ReportTestData.Report(ReportTestData.Requester, status);
        repository.Given(own);

        IActionResult result = await ControllerFor(service, ReportTestData.Requester, "Employee").ExcelAsync(own.Id);

        Assert.IsNotInstanceOfType<FileResult>(result);
        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status409Conflict, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
        Assert.DoesNotContain("totalAmount", JsonSerializer.Serialize(problem, _webJson), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scenario 8: the Excel of a completed report is a real xlsx with the content type and the file name of the contract, and every count and amount of the table is a numeric cell equal to the saved result.</summary>
    [TestMethod]
    public async Task Scenario8_CompletedExcel_HasTheContractAndTheCellsOfTheSavedResult()
    {
        (ReportService service, FakeReportRepository repository, _) = CreateWeb();
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        repository.Given(report);
        ReportSummary saved = ReportResultSerializer.Deserialize(report.ResultJson!);

        IActionResult result = await ControllerFor(service, ReportTestData.Requester, "Employee").ExcelAsync(report.Id);

        FileContentResult file = Assert.IsInstanceOfType<FileContentResult>(result);
        Assert.AreEqual("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.AreEqual($"report-{report.Id}.xlsx", file.FileDownloadName);
        Dictionary<string, (bool IsNumber, string Text)> cells = ReadCells(file.FileContents);
        Assert.AreEqual(report.Id.ToString(), cells["B1"].Text);
        Assert.AreEqual("Mine", cells["B2"].Text);
        Assert.AreEqual("2026-09-01", cells["B3"].Text);
        Assert.AreEqual("2026-09-30", cells["B4"].Text);

        for (int index = 0; index < saved.ByStatus.Count; index++)
        {
            string row = (8 + index).ToString(CultureInfo.InvariantCulture);
            Assert.AreEqual(saved.ByStatus[index].Status.ToString(), cells["A" + row].Text);
            AssertNumber(cells, "B" + row, saved.ByStatus[index].Count);
            AssertNumber(cells, "C" + row, saved.ByStatus[index].Amount);
        }

        Assert.AreEqual("Total geral", cells["A13"].Text);
        AssertNumber(cells, "B13", saved.TotalCount);
        AssertNumber(cells, "C13", saved.TotalAmount);
        AssertNumber(cells, "B13", 3);
        AssertNumber(cells, "C13", 175.00m);
    }

    /// <summary>Scenario 7: the result that the worker saved is what the web gives back, and it does not change when a reimbursement changes afterwards: the detail and the Excel stay the same, and no expense is read.</summary>
    [TestMethod]
    public async Task Scenario7_WorkerResult_SurvivesAnExpenseChange_ForTheDetailAndTheExcel()
    {
        ProcessedReport processed = await RequestAndProcessAsync();
        (ReportsController reader, FakeReportRepository readData) = WebReading(processed.Store, processed.Id);
        ActionResult<ReportResponse> before = await reader.GetAsync(processed.Id);
        string detailBefore = JsonSerializer.Serialize(before.Value, _webJson);
        string[] excelBefore = CellTexts(await reader.ExcelAsync(processed.Id));

        ChangeAnExpense(processed.WorkerData);
        ChangeAnExpense(readData);

        ActionResult<ReportResponse> after = await reader.GetAsync(processed.Id);
        ReportResponse response = after.Value!;
        Assert.AreEqual("Completed", response.Status);
        Assert.AreEqual(3, response.Result!.TotalCount);
        Assert.AreEqual(175.00m, response.Result.TotalAmount);
        Assert.AreEqual(detailBefore, JsonSerializer.Serialize(response, _webJson));
        CollectionAssert.AreEqual(excelBefore, CellTexts(await reader.ExcelAsync(processed.Id)));
        Assert.AreEqual(0, readData.ExpenseReads);
    }

    /// <summary>Scenario 6: the same message again, even after the worker restarted and a reimbursement changed, is a duplicate with no new result: the saved state is identical, nothing is written or read, and the web gives the same detail.</summary>
    [TestMethod]
    public async Task Scenario6_SameMessageAgain_AfterTheExpenseChanged_LeavesTheSavedResultUntouched()
    {
        ProcessedReport processed = await RequestAndProcessAsync();
        ReportRequest saved = processed.Store.Saved(processed.Id);
        (ReportsController reader, _) = WebReading(processed.Store, processed.Id);
        string detailBefore = JsonSerializer.Serialize((await reader.GetAsync(processed.Id)).Value, _webJson);
        ChangeAnExpense(processed.WorkerData);
        (ReportMessageProcessor restarted, _, FakeReportRepository restartedData) = CreateWorker(processed.Store);
        ChangeAnExpense(restartedData);

        ReportMessageOutcome again = await processed.Processor.HandleAsync(ReportMessage.Serialize(processed.Id), CancellationToken.None);
        ReportMessageOutcome afterRestart = await restarted.HandleAsync(ReportMessage.Serialize(processed.Id), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Duplicate, again);
        Assert.AreEqual(ReportMessageOutcome.Duplicate, afterRestart);
        ReportRequest after = processed.Store.Saved(processed.Id);
        Assert.AreEqual(ReportStatus.Completed, after.Status);
        Assert.AreEqual(saved.ResultJson, after.ResultJson);
        Assert.AreEqual(saved.StartedAtUtc, after.StartedAtUtc);
        Assert.AreEqual(saved.CompletedAtUtc, after.CompletedAtUtc);
        Assert.AreEqual(2, processed.Store.AppliedWrites);
        Assert.AreEqual(1, processed.WorkerData.ExpenseReads);
        Assert.AreEqual(0, restartedData.ExpenseReads);
        (ReportsController readerAfter, _) = WebReading(processed.Store, processed.Id);
        Assert.AreEqual(detailBefore, JsonSerializer.Serialize((await readerAfter.GetAsync(processed.Id)).Value, _webJson));
    }

    /// <summary>Scenario 10: when the broker does not confirm (or cannot be reached), the answer is a 503 that carries the identifier of the saved report, the report stays pending and is never failed, and it can be followed by that identifier.</summary>
    /// <param name="brokerUnreachable"><c>true</c> when the broker cannot be reached, <c>false</c> when it answers without confirming.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Scenario10_BrokerDoesNotConfirm_Is503WithTheIdAndTheReportStaysPending(bool brokerUnreachable)
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = CreateWeb();
        publisher.ThrowOnPublish = brokerUnreachable;
        publisher.Result = ReportPublishResult.NotConfirmed;
        ReportsController controller = ControllerFor(service, ReportTestData.Requester, "Employee");

        IActionResult result = await controller.RequestAsync(ValidBody("Mine"));

        Assert.IsNotInstanceOfType<AcceptedResult>(result);
        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
        ReportRequest saved = repository.Reports.Single();
        Assert.AreEqual(saved.Id, problem.Extensions["id"]);
        Assert.AreEqual(ReportStatus.Pending, saved.Status);
        ReportResponse followed = (await controller.GetAsync(saved.Id)).Value!;
        Assert.AreEqual("Pending", followed.Status);
        Assert.IsNull(followed.Error);
        Assert.IsNull(followed.Result);
    }

    /// <summary>Scenario 12: a body that carries an owner and a requester is read, and the saved request belongs to the authenticated user; the intruder named in the body is nowhere in the saved request or in the answer.</summary>
    [TestMethod]
    public async Task Scenario12_BodyWithOwnerIdAndRequesterId_IsIgnored_TheSavedRequesterIsTheCaller()
    {
        (ReportService service, FakeReportRepository repository, _) = CreateWeb();
        const string json = "{\"fromDate\":\"2026-09-01\",\"toDate\":\"2026-09-30\",\"scope\":\"Mine\",\"ownerId\":\"intruder\",\"requesterId\":\"intruder\"}";
        CreateReportRequest body = JsonSerializer.Deserialize<CreateReportRequest>(json, _webJson)!;

        IActionResult result = await ControllerFor(service, ReportTestData.Requester, "Employee").RequestAsync(body);

        AcceptedResult accepted = Assert.IsInstanceOfType<AcceptedResult>(result);
        ReportRequest saved = repository.Reports.Single();
        Assert.AreEqual(ReportTestData.Requester, saved.RequesterId);
        Assert.IsFalse(repository.Reports.Any(report => report.RequesterId == "intruder"));
        Assert.DoesNotContain("intruder", JsonSerializer.Serialize(accepted.Value, _webJson));
    }

    private static (ReportService Service, FakeReportRepository Repository, FakeReportPublisher Publisher) CreateWeb(FakeReportRepository? repository = null)
    {
        repository ??= new FakeReportRepository();
        var publisher = new FakeReportPublisher(repository);
        var service = new ReportService(repository, publisher, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportService>.Instance);

        return (service, repository, publisher);
    }

    private static (ReportMessageProcessor Processor, FakeReportProcessingStore Store, FakeReportRepository WorkerData) CreateWorker(FakeReportProcessingStore? store = null)
    {
        var workerData = new FakeReportRepository();
        workerData.Expenses.AddRange(ReportTestData.ExampleExpenses());
        store ??= new FakeReportProcessingStore();
        var processor = new ReportMessageProcessor(store, workerData, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportMessageProcessor>.Instance);

        return (processor, store, workerData);
    }

    private static ReportsController ControllerFor(ReportService service, string userId, params string[] roles)
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));

        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return new ReportsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
            ProblemDetailsFactory = new FakeProblemDetailsFactory(),
        };
    }

    private static CreateReportRequest ValidBody(string scope)
    {
        return new CreateReportRequest { FromDate = ReportTestData.From, ToDate = ReportTestData.To, Scope = scope };
    }

    private static DateOnly? ParseDate(string? text)
    {
        return text is null ? null : DateOnly.Parse(text, CultureInfo.InvariantCulture);
    }

    /// <summary>The web registers and publishes, then the worker handles the message about the request that the web saved.</summary>
    private static async Task<ProcessedReport> RequestAndProcessAsync()
    {
        (ReportService service, FakeReportRepository repository, FakeReportPublisher publisher) = CreateWeb();
        IActionResult accepted = await ControllerFor(service, ReportTestData.Requester, "Employee").RequestAsync(ValidBody("Mine"));
        Assert.IsInstanceOfType<AcceptedResult>(accepted);
        Guid id = publisher.Published.Single();

        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository workerData) = CreateWorker();
        store.Given(repository.Reports.Single());
        ReportMessageOutcome outcome = await processor.HandleAsync(ReportMessage.Serialize(id), CancellationToken.None);
        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);

        return new ProcessedReport(id, processor, store, workerData);
    }

    /// <summary>The web reads what the worker saved: the same state, seen from another process, with the expenses it could read.</summary>
    private static (ReportsController Controller, FakeReportRepository Data) WebReading(FakeReportProcessingStore store, Guid id)
    {
        var data = new FakeReportRepository();
        data.Given(store.Saved(id));
        data.Expenses.AddRange(ReportTestData.ExampleExpenses());
        (ReportService service, _, _) = CreateWeb(data);

        return (ControllerFor(service, ReportTestData.Requester, "Employee"), data);
    }

    /// <summary>Changes the state of one reimbursement and the amount of another, as someone would after the report was completed.</summary>
    private static void ChangeAnExpense(FakeReportRepository data)
    {
        data.Expenses[0].Status = ExpenseStatus.Rejected;
        data.Expenses[1].Amount = 9999.99m;
    }

    private static ProblemDetails NotFoundProblem(IActionResult? result)
    {
        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, objectResult.StatusCode);

        return Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
    }

    private static void AssertSameProblem(ProblemDetails expected, ProblemDetails actual)
    {
        Assert.AreEqual(expected.Status, actual.Status);
        Assert.AreEqual(expected.Title, actual.Title);
        Assert.AreEqual(expected.Detail, actual.Detail);
        Assert.AreEqual(expected.Type, actual.Type);
        Assert.AreEqual(expected.Instance, actual.Instance);
        Assert.HasCount(expected.Extensions.Count, actual.Extensions);
    }

    private static void AssertCarriesNoDataOf(ReportRequest report, ProblemDetails problem)
    {
        string body = JsonSerializer.Serialize(problem, _webJson);

        Assert.DoesNotContain(report.Id.ToString(), body);
        Assert.DoesNotContain(report.RequesterId, body);
        Assert.DoesNotContain("175", body);
        Assert.DoesNotContain("result", body, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] CellTexts(IActionResult result)
    {
        FileContentResult file = Assert.IsInstanceOfType<FileContentResult>(result);

        return ReadCells(file.FileContents).OrderBy(cell => cell.Key, StringComparer.Ordinal).Select(cell => cell.Value.Text).ToArray();
    }

    private static Dictionary<string, (bool IsNumber, string Text)> ReadCells(byte[] bytes)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        WorksheetPart sheet = document.WorkbookPart!.WorksheetParts.Single();
        var cells = new Dictionary<string, (bool IsNumber, string Text)>();

        foreach (Cell cell in sheet.Worksheet!.Descendants<Cell>())
        {
            EnumValue<CellValues>? dataType = cell.DataType;
            bool isNumber = (dataType is null || dataType.Value == CellValues.Number) && cell.InlineString is null;
            cells[cell.CellReference!.Value!] = (isNumber, cell.InlineString?.Text?.Text ?? cell.CellValue?.Text ?? string.Empty);
        }

        return cells;
    }

    private static void AssertNumber(Dictionary<string, (bool IsNumber, string Text)> cells, string reference, decimal expected)
    {
        (bool isNumber, string text) = cells[reference];

        Assert.IsTrue(isNumber, $"{reference} must be a numeric cell.");
        Assert.AreEqual(expected, decimal.Parse(text, CultureInfo.InvariantCulture), $"{reference} must hold the saved value.");
    }

    private sealed class ProcessedReport
    {
        public ProcessedReport(Guid id, ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository workerData)
        {
            Id = id;
            Processor = processor;
            Store = store;
            WorkerData = workerData;
        }

        public Guid Id { get; }

        public ReportMessageProcessor Processor { get; }

        public FakeReportProcessingStore Store { get; }

        public FakeReportRepository WorkerData { get; }
    }
}
