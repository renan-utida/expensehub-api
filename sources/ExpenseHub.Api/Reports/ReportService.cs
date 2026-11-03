using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Rules of requesting a report, of following it, and of downloading its Excel file. The requester, the identifier, the state
/// and the instants always come from the caller (the token) and the server clock, never from the client data. Reading is
/// always limited to the requests of the caller: a request of someone else looks the same as one that does not exist.
/// Reading never calculates anything: the result is the one that was saved. Which role may ask for which scope is decided by
/// <see cref="ReportAccess"/>; reading needs no role, only ownership.
/// </summary>
public partial class ReportService
{
    private readonly IReportRepository _repository;
    private readonly IReportPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportService"/> class.
    /// </summary>
    /// <param name="repository">The report storage.</param>
    /// <param name="publisher">The publisher of the message to the worker.</param>
    /// <param name="timeProvider">The clock of the server.</param>
    /// <param name="logger">The logger.</param>
    public ReportService(
        IReportRepository repository,
        IReportPublisher publisher,
        TimeProvider timeProvider,
        ILogger<ReportService> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Registers a report request and publishes its message. The answers follow this order: no role of the caller may request
    /// a report (forbidden), invalid data (nothing is saved and nothing is published), the roles of the caller do not allow
    /// the scope (forbidden), the storage fails (no request and no identifier exist), the request is saved, then the
    /// broker confirms the message (accepted) or does not (the request stays <c>Pending</c> and is not marked as failed).
    /// The order is the one of the endpoint, so the rule does not depend on the attribute of the route alone.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token. It becomes the requester.</param>
    /// <param name="details">The fields chosen by the client.</param>
    /// <returns>The saved request, or the reason it was not accepted.</returns>
    public virtual async Task<ReportOperationResult> RequestAsync(ExpenseCaller caller, ReportRequestDetails details)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);
        ArgumentNullException.ThrowIfNull(details);

        if (!ReportAccess.HasRequestRole(caller))
        {
            LogRoleRefused();

            return ReportOperationResult.Forbidden();
        }

        IReadOnlyList<ReportValidationError> errors = ReportRules.Validate(details);

        if (errors.Count > 0 || !ReportRules.TryParseScope(details.Scope, out ReportScope scope))
        {
            return ReportOperationResult.Invalid(errors);
        }

        if (!ReportAccess.CanRequest(caller, scope))
        {
            LogScopeRefused(scope);

            return ReportOperationResult.Forbidden();
        }

        var report = new ReportRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = caller.UserId,
            Scope = scope,
            FromDate = details.FromDate!.Value,
            ToDate = details.ToDate!.Value,
            Status = ReportStatus.Pending,
            CreatedAtUtc = _timeProvider.GetUtcNow(),
        };

        try
        {
            await _repository.AddAsync(report);
        }
        catch (ReportStorageException exception)
        {
            LogStorageUnavailable(exception.InnerException?.GetType().Name ?? exception.GetType().Name);

            return ReportOperationResult.StorageUnavailable();
        }

        ReportPublishResult published;

        try
        {
            published = await _publisher.PublishAsync(report.Id, CancellationToken.None);
        }
        catch (ReportPublishException exception)
        {
            LogPublishFailed(report.Id, exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            published = ReportPublishResult.NotConfirmed;
        }

        if (published != ReportPublishResult.Confirmed)
        {
            LogNotConfirmed(report.Id);

            return ReportOperationResult.NotPublished(report);
        }

        LogPublished(report.Id);

        return ReportOperationResult.Accept(report);
    }

    /// <summary>
    /// Lists the requests made by the caller, newest first. Requests of other users are never loaded.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <returns>The requests of the caller.</returns>
    public virtual async Task<IReadOnlyList<ReportRequest>> ListAsync(ExpenseCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);

        return await _repository.ListByRequesterAsync(caller.UserId);
    }

    /// <summary>
    /// Gets one request made by the caller, with the result that was saved. It does not calculate or read any expense.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="reportId">The identifier of the request.</param>
    /// <returns>The request, or <c>null</c> when it does not exist or belongs to someone else.</returns>
    public virtual async Task<ReportRequest?> GetAsync(ExpenseCaller caller, Guid reportId)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);

        return await _repository.FindByRequesterAsync(reportId, caller.UserId);
    }

    /// <summary>
    /// Builds the Excel file of a completed request of the caller from the result that was saved. It does not read any expense
    /// and publishes no message. The answers follow this order: the request does not exist or is not the caller's
    /// (<c>404</c>), the request is not completed (<c>409</c>, which includes a failed one), and the file is ready.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="reportId">The identifier of the request.</param>
    /// <returns>The file, or the reason there is none.</returns>
    public virtual async Task<ReportExcelResult> GetExcelAsync(ExpenseCaller caller, Guid reportId)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);

        ReportRequest? report = await _repository.FindByRequesterAsync(reportId, caller.UserId);

        if (report is null)
        {
            return ReportExcelResult.NotFound();
        }

        if (report.Status != ReportStatus.Completed || report.ResultJson is null)
        {
            return ReportExcelResult.NotCompleted();
        }

        ReportSummary summary = ReportResultSerializer.Deserialize(report.ResultJson);

        return ReportExcelResult.Ready(ReportExcelWriter.FileNameFor(report.Id), ReportExcelWriter.Build(report, summary));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A report request was refused: no role of the user may request a report.")]
    private partial void LogRoleRefused();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A report request was refused: the roles of the user may not request the scope {Scope}.")]
    private partial void LogScopeRefused(ReportScope scope);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} registered and published.")]
    private partial void LogPublished(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} registered, but the broker did not confirm the message. The report stays pending.")]
    private partial void LogNotConfirmed(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} registered, but publishing the message failed ({ErrorType}).")]
    private partial void LogPublishFailed(Guid reportId, string errorType);

    [LoggerMessage(Level = LogLevel.Error, Message = "A report could not be registered because the storage is unavailable ({ErrorType}).")]
    private partial void LogStorageUnavailable(string errorType);
}
