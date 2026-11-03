using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Asynchronous reports of expenses by period: requesting one, following its progress and downloading its Excel file.
/// Every route needs an authenticated user, and the user only sees the reports he or she requested. The requester always
/// comes from the token, never from the body. Which role may ask for which scope is decided by the authorization issue (CP3-I02).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private const string NotFoundTitle = "Report not found.";
    private const string NotCompletedTitle = "The report is not completed.";
    private const string NotPublishedTitle = "The report was registered, but it could not be sent for processing.";
    private const string StorageUnavailableTitle = "The report could not be registered.";

    private readonly ReportService _reports;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="reports">The report service.</param>
    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// Requests a report of the expenses of a period. The server registers the request for the authenticated user, then
    /// publishes the message to the worker, and only answers <c>202</c> after the broker confirmed it.
    /// </summary>
    /// <param name="request">The first day, the last day and the scope.</param>
    /// <returns>
    /// <c>202</c> with the <c>Location</c> of the report and its envelope; <c>400</c> for invalid dates or scope, with nothing
    /// saved and nothing published; <c>401</c> without a token; <c>503</c> with the identifier of the saved report when the
    /// broker did not confirm the message (the report stays pending), or <c>503</c> without an identifier when the report
    /// could not be saved.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> RequestAsync([FromBody] CreateReportRequest request)
    {
        var details = new ReportRequestDetails(request.FromDate, request.ToDate, request.Scope);
        ReportOperationResult result = await _reports.RequestAsync(GetCaller(), details);

        switch (result.Status)
        {
            case ReportOperationStatus.Accepted:
                ReportResponse accepted = ReportResponse.From(result.Report!);

                return Accepted($"/api/reports/{accepted.Id}", accepted);

            case ReportOperationStatus.ValidationFailed:
                foreach (ReportValidationError error in result.Errors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                return ValidationProblem(ModelState);

            case ReportOperationStatus.NotPublished:
                ProblemDetails notPublished = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: NotPublishedTitle,
                    detail: "Follow the report by its identifier. A new request creates another report.");
                notPublished.Extensions["id"] = result.Report!.Id;

                return StatusCode(StatusCodes.Status503ServiceUnavailable, notPublished);

            default:
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: StorageUnavailableTitle);
        }
    }

    /// <summary>
    /// Lists the reports the authenticated user requested, newest first. Reports of other users never appear.
    /// </summary>
    /// <returns>
    /// <c>200</c> with the envelopes of the reports of the user; <c>401</c> without a token.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportResponse>>> ListAsync()
    {
        IReadOnlyList<ReportRequest> reports = await _reports.ListAsync(GetCaller());

        return reports.Select(ReportResponse.From).ToList();
    }

    /// <summary>
    /// Gets the progress and the saved result of one report the authenticated user requested. A report that does not exist
    /// and one that belongs to someone else get the same <c>404</c>.
    /// </summary>
    /// <param name="id">The identifier of the report.</param>
    /// <returns>
    /// <c>200</c> with the envelope; <c>404</c> when the report does not exist or is not the user's; <c>401</c> without a token.
    /// </returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportResponse>> GetAsync(Guid id)
    {
        ReportRequest? report = await _reports.GetAsync(GetCaller(), id);

        return report is null ? ReportNotFound() : ReportResponse.From(report);
    }

    /// <summary>
    /// Downloads the Excel file (<c>.xlsx</c>) of a completed report the authenticated user requested. The file is built from
    /// the result that was saved; nothing is calculated again.
    /// </summary>
    /// <param name="id">The identifier of the report.</param>
    /// <returns>
    /// <c>200</c> with the file (<c>report-{id}.xlsx</c>, as an attachment); <c>404</c> when the report does not exist or is
    /// not the user's; <c>409</c> when the report is the user's own but is not completed (pending, processing or failed);
    /// <c>401</c> without a token.
    /// </returns>
    [HttpGet("{id:guid}/excel")]
    public async Task<IActionResult> ExcelAsync(Guid id)
    {
        ReportExcelResult result = await _reports.GetExcelAsync(GetCaller(), id);

        switch (result.Status)
        {
            case ReportExcelStatus.Ready:
                return File(result.Content.ToArray(), ReportExcelWriter.ContentType, result.FileName);

            case ReportExcelStatus.NotCompleted:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: NotCompletedTitle);

            default:
                return ReportNotFound();
        }
    }

    private ExpenseCaller GetCaller()
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The token has no user identifier.");
        string[] roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        return new ExpenseCaller(userId, roles);
    }

    private ObjectResult ReportNotFound()
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: NotFoundTitle);
    }
}
