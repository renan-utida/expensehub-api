using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Controllers;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of what the reports controller does by itself, with the service replaced by a fake: which status code and body it
/// gives to each outcome of the service, what it passes to the service (the user from the claims, never a requester from the
/// body, the identifier, the fields) and which headers it sets. The rules of the service have their own tests.
/// </summary>
[TestClass]
public sealed class ReportsControllerTests
{
    private const string UserId = "user-1";

    /// <summary>An accepted request is answered with 202, the Location of the report and the pending envelope.</summary>
    [TestMethod]
    public async Task RequestAsync_Accepted_Returns202WithLocationAndTheEnvelope()
    {
        var service = new FakeReportService();
        ReportRequest report = ReportTestData.Report(UserId);
        service.RequestResult = ReportOperationResult.Accept(report);

        IActionResult result = await NewController(service, UserId, "Employee").RequestAsync(ValidBody());

        AcceptedResult accepted = Assert.IsInstanceOfType<AcceptedResult>(result);
        Assert.AreEqual(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.AreEqual($"/api/reports/{report.Id}", accepted.Location);
        ReportResponse envelope = Assert.IsInstanceOfType<ReportResponse>(accepted.Value);
        Assert.AreEqual(report.Id, envelope.Id);
        Assert.AreEqual("Pending", envelope.Status);
        Assert.IsNull(envelope.Result);
    }

    /// <summary>The controller passes the user of the claims and the fields of the body, and the only call is the request.</summary>
    [TestMethod]
    public async Task RequestAsync_ForwardsTheUserOfTheClaimsAndTheFieldsOfTheBody()
    {
        var service = new FakeReportService { RequestResult = ReportOperationResult.Accept(ReportTestData.Report(UserId)) };

        await NewController(service, UserId, "Auditor").RequestAsync(ValidBody("Global"));

        CollectionAssert.AreEqual(new[] { nameof(ReportService.RequestAsync) }, service.Calls.ToArray());
        Assert.AreEqual(UserId, service.LastCaller!.UserId);
        Assert.IsTrue(service.LastCaller.IsInRole("Auditor"));
        Assert.AreEqual(ReportTestData.From, service.LastDetails!.FromDate);
        Assert.AreEqual(ReportTestData.To, service.LastDetails.ToDate);
        Assert.AreEqual("Global", service.LastDetails.Scope);
    }

    /// <summary>Data the service finds invalid is answered with 400 and the error of each field.</summary>
    [TestMethod]
    public async Task RequestAsync_ServiceReportsInvalidData_Returns400WithTheFieldErrors()
    {
        var service = new FakeReportService
        {
            RequestResult = ReportOperationResult.Invalid([new ReportValidationError("FromDate", "The start date cannot be after the end date.")]),
        };

        IActionResult result = await NewController(service, UserId).RequestAsync(ValidBody());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        ValidationProblemDetails problem = Assert.IsInstanceOfType<ValidationProblemDetails>(objectResult.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        string[] expected = ["The start date cannot be after the end date."];
        CollectionAssert.AreEqual(expected, problem.Errors["FromDate"]);
    }

    /// <summary>When the broker does not confirm, the answer is 503 with the identifier of the saved report and no result; it is not a 202.</summary>
    [TestMethod]
    public async Task RequestAsync_NotPublished_Returns503WithTheIdentifierOfTheSavedReport()
    {
        var service = new FakeReportService();
        ReportRequest report = ReportTestData.Report(UserId);
        service.RequestResult = ReportOperationResult.NotPublished(report);

        IActionResult result = await NewController(service, UserId).RequestAsync(ValidBody());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.AreEqual(report.Id, problem.Extensions["id"]);
        Assert.IsNotNull(problem.Title);
    }

    /// <summary>When the report could not be saved, the answer is an explicit 503 with no identifier at all.</summary>
    [TestMethod]
    public async Task RequestAsync_StorageUnavailable_Returns503WithoutAnIdentifier()
    {
        var service = new FakeReportService { RequestResult = ReportOperationResult.StorageUnavailable() };

        IActionResult result = await NewController(service, UserId).RequestAsync(ValidBody());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
        Assert.IsFalse(problem.Extensions.ContainsKey("id"));
    }

    /// <summary>A request that was not published is never answered with 202.</summary>
    /// <param name="outcome">The outcome of the service.</param>
    [TestMethod]
    [DataRow(ReportOperationStatus.NotPublished)]
    [DataRow(ReportOperationStatus.StorageUnavailable)]
    [DataRow(ReportOperationStatus.ValidationFailed)]
    public async Task RequestAsync_AnythingButAccepted_IsNeverA202(ReportOperationStatus outcome)
    {
        var service = new FakeReportService();
        service.RequestResult = outcome switch
        {
            ReportOperationStatus.NotPublished => ReportOperationResult.NotPublished(ReportTestData.Report(UserId)),
            ReportOperationStatus.StorageUnavailable => ReportOperationResult.StorageUnavailable(),
            _ => ReportOperationResult.Invalid([new ReportValidationError("Scope", "The scope is required.")]),
        };

        IActionResult result = await NewController(service, UserId).RequestAsync(ValidBody());

        Assert.IsNotInstanceOfType<AcceptedResult>(result);
        Assert.AreNotEqual(StatusCodes.Status202Accepted, Assert.IsInstanceOfType<ObjectResult>(result).StatusCode);
    }

    /// <summary>The list answers with the envelopes of the requests the service returned.</summary>
    [TestMethod]
    public async Task ListAsync_ReturnsTheEnvelopesAndCallsOnlyTheList()
    {
        var service = new FakeReportService { ListResult = [ReportTestData.Report(UserId), ReportTestData.CompletedReport(UserId)] };

        ActionResult<IReadOnlyList<ReportResponse>> result = await NewController(service, UserId, "Employee").ListAsync();

        Assert.HasCount(2, result.Value!);
        CollectionAssert.AreEqual(new[] { nameof(ReportService.ListAsync) }, service.Calls.ToArray());
        Assert.AreEqual(UserId, service.LastCaller!.UserId);
    }

    /// <summary>The detail of a report the service returned is answered with its envelope, and the identifier of the route is passed on.</summary>
    [TestMethod]
    public async Task GetAsync_Found_ReturnsTheEnvelope()
    {
        var service = new FakeReportService();
        ReportRequest report = ReportTestData.CompletedReport(UserId);
        service.GetResult = report;

        ActionResult<ReportResponse> result = await NewController(service, UserId).GetAsync(report.Id);

        Assert.AreEqual(report.Id, result.Value!.Id);
        Assert.AreEqual("Completed", result.Value.Status);
        Assert.AreEqual(report.Id, service.LastReportId);
        CollectionAssert.AreEqual(new[] { nameof(ReportService.GetAsync) }, service.Calls.ToArray());
    }

    /// <summary>A report that is missing or belongs to someone else is answered with 404 and no data.</summary>
    [TestMethod]
    public async Task GetAsync_NotFound_Returns404()
    {
        var service = new FakeReportService { GetResult = null };

        ActionResult<ReportResponse> result = await NewController(service, UserId).GetAsync(Guid.NewGuid());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.IsNull(result.Value);
    }

    /// <summary>A ready file is answered with 200, the content type of an xlsx and the name report-{id}.xlsx as an attachment.</summary>
    [TestMethod]
    public async Task ExcelAsync_Ready_ReturnsTheFileWithTheContract()
    {
        var service = new FakeReportService();
        var id = Guid.NewGuid();
        byte[] content = [1, 2, 3, 4];
        service.ExcelResult = ReportExcelResult.Ready(ReportExcelWriter.FileNameFor(id), content);

        IActionResult result = await NewController(service, UserId).ExcelAsync(id);

        FileContentResult file = Assert.IsInstanceOfType<FileContentResult>(result);
        Assert.AreEqual("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.AreEqual($"report-{id}.xlsx", file.FileDownloadName);
        CollectionAssert.AreEqual(content, file.FileContents);
        Assert.AreEqual(id, service.LastReportId);
    }

    /// <summary>An own report that is not completed is a 409, with no file.</summary>
    [TestMethod]
    public async Task ExcelAsync_NotCompleted_Returns409()
    {
        var service = new FakeReportService { ExcelResult = ReportExcelResult.NotCompleted() };

        IActionResult result = await NewController(service, UserId).ExcelAsync(Guid.NewGuid());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    /// <summary>A report that is missing or belongs to someone else is a 404, with no file.</summary>
    [TestMethod]
    public async Task ExcelAsync_NotFound_Returns404()
    {
        var service = new FakeReportService { ExcelResult = ReportExcelResult.NotFound() };

        IActionResult result = await NewController(service, UserId).ExcelAsync(Guid.NewGuid());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    /// <summary>A principal without a user identifier is refused before the service is called, so a report never has an unknown requester.</summary>
    [TestMethod]
    public async Task Action_PrincipalWithoutUserIdentifier_ThrowsAndDoesNotCallTheService()
    {
        var service = new FakeReportService();
        ReportsController controller = NewController(service, new ClaimsPrincipal(new ClaimsIdentity("test")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.RequestAsync(ValidBody()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ExcelAsync(Guid.NewGuid()));

        Assert.IsEmpty(service.Calls);
    }

    /// <summary>The requested scope is never used to pick the requester: even a request for Global is registered for the user of the claims.</summary>
    [TestMethod]
    public async Task RequestAsync_GlobalScope_StillUsesTheUserOfTheClaims()
    {
        var service = new FakeReportService { RequestResult = ReportOperationResult.Accept(ReportTestData.Report(UserId, scope: ReportScope.Global)) };

        await NewController(service, UserId).RequestAsync(ValidBody("Global"));

        Assert.AreEqual(UserId, service.LastCaller!.UserId);
    }

    private static ReportsController NewController(FakeReportService service, string userId, params string[] roles)
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));

        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return NewController(service, new ClaimsPrincipal(identity));
    }

    private static ReportsController NewController(FakeReportService service, ClaimsPrincipal principal)
    {
        return new ReportsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
            ProblemDetailsFactory = new FakeProblemDetailsFactory(),
        };
    }

    private static CreateReportRequest ValidBody(string scope = "Mine")
    {
        return new CreateReportRequest { FromDate = ReportTestData.From, ToDate = ReportTestData.To, Scope = scope };
    }
}
