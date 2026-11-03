using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// How the processing of one message ended. The consumer maps it to an acknowledgement through <see cref="ReportDelivery"/>.
/// </summary>
public enum ReportMessageOutcome
{
    /// <summary>The report was calculated and saved as <c>Completed</c> with its result, in one write.</summary>
    Completed,

    /// <summary>The report was already finished (<c>Completed</c> or <c>Failed</c>), or another worker finished it first. Nothing was changed.</summary>
    Duplicate,

    /// <summary>The report cannot be calculated and was saved as <c>Failed</c> with a stable code and a sanitized message.</summary>
    PermanentFailure,

    /// <summary>The message is not a valid report message, or the report does not exist, so there is nothing to process or to save.</summary>
    Discarded,

    /// <summary>The storage is temporarily unavailable, so nothing that is needed is saved. The message must come back.</summary>
    TransientFailure,
}

/// <summary>
/// Handles one message of the report queue: reads the identifier, loads the saved request, calculates the report and saves the
/// result, and tells how it ended. It never acknowledges anything itself; it returns an outcome, and an outcome that means
/// "saved" or "already saved" is only returned after the storage confirmed the write. The rules:
/// <list type="bullet">
/// <item>The period, the scope and the requester come from the saved request, never from the message.</item>
/// <item>A request that is already <c>Completed</c> or <c>Failed</c> is a duplicate: nothing is calculated and nothing is written,
/// and because the proof is the saved state, it holds after the worker restarts.</item>
/// <item>A <c>Pending</c> request becomes <c>Processing</c> by a conditional write. A request that is already <c>Processing</c>
/// (for example, a worker stopped halfway) is resumed: the calculation is repeated, and the final write is conditional on
/// <c>Processing</c>, so the effect is still saved once.</item>
/// <item><c>Completed</c> and the result are saved in the same write.</item>
/// <item>A request that can never be calculated becomes <c>Failed</c> with a stable code and a fixed message, and its message is
/// not put back. If saving the failure also fails, the outcome is transient, because the failure is not saved.</item>
/// <item>When the storage is temporarily unavailable the outcome is transient, and no state is made up.</item>
/// </list>
/// </summary>
public sealed partial class ReportMessageProcessor
{
    private readonly IReportProcessingStore _store;
    private readonly IReportRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReportMessageProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportMessageProcessor"/> class.
    /// </summary>
    /// <param name="store">The state of the report requests.</param>
    /// <param name="repository">The reading of the expenses of a report.</param>
    /// <param name="timeProvider">The clock of the server.</param>
    /// <param name="logger">The logger.</param>
    public ReportMessageProcessor(
        IReportProcessingStore store,
        IReportRepository repository,
        TimeProvider timeProvider,
        ILogger<ReportMessageProcessor> logger)
    {
        _store = store;
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Processes the body of one message.
    /// </summary>
    /// <param name="body">The body of the message.</param>
    /// <param name="cancellationToken">Stops the processing, for example when the worker is shutting down.</param>
    /// <returns>How the processing ended.</returns>
    public async Task<ReportMessageOutcome> HandleAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (!ReportMessage.TryParse(body, out Guid reportId))
        {
            LogUnreadable();

            return ReportMessageOutcome.Discarded;
        }

        try
        {
            return await ProcessAsync(reportId, cancellationToken);
        }
        catch (ReportStorageException exception)
        {
            LogTransient(reportId, exception.InnerException?.GetType().Name ?? exception.GetType().Name);

            return ReportMessageOutcome.TransientFailure;
        }
    }

    private static bool IsFinished(ReportStatus status)
    {
        return status is ReportStatus.Completed or ReportStatus.Failed;
    }

    private async Task<ReportMessageOutcome> ProcessAsync(Guid reportId, CancellationToken cancellationToken)
    {
        ReportRequest? report = await _store.FindAsync(reportId);

        if (report is null)
        {
            LogNotFound(reportId);

            return ReportMessageOutcome.Discarded;
        }

        if (IsFinished(report.Status))
        {
            LogDuplicate(reportId, report.Status);

            return ReportMessageOutcome.Duplicate;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (report.Status == ReportStatus.Pending)
        {
            if (await _store.TryStartAsync(reportId, _timeProvider.GetUtcNow()))
            {
                LogStarted(reportId, false);
            }
            else
            {
                // Another attempt moved the request first: look again and decide on what is saved now.
                ReportRequest? current = await _store.FindAsync(reportId);

                if (current is null)
                {
                    LogNotFound(reportId);

                    return ReportMessageOutcome.Discarded;
                }

                if (IsFinished(current.Status))
                {
                    LogDuplicate(reportId, current.Status);

                    return ReportMessageOutcome.Duplicate;
                }

                if (current.Status == ReportStatus.Pending)
                {
                    // The start did not apply and the request is still pending: nothing is saved, so the message must come back.
                    LogTransient(reportId, nameof(ReportStatus.Pending));

                    return ReportMessageOutcome.TransientFailure;
                }

                report = current;
                LogStarted(reportId, true);
            }
        }
        else
        {
            LogStarted(reportId, true);
        }

        cancellationToken.ThrowIfCancellationRequested();

        Calculation calculation = await CalculateAsync(report);

        cancellationToken.ThrowIfCancellationRequested();

        if (calculation.ErrorCode is not null)
        {
            return await FailAsync(reportId, calculation.ErrorCode, calculation.ErrorMessage!);
        }

        if (await _store.TryCompleteAsync(reportId, calculation.ResultJson!, _timeProvider.GetUtcNow()))
        {
            LogCompleted(reportId, calculation.TotalCount);

            return ReportMessageOutcome.Completed;
        }

        return await ConfirmFinishedAsync(reportId);
    }

    private async Task<Calculation> CalculateAsync(ReportRequest report)
    {
        ReportQuery query;

        try
        {
            query = ReportQuery.From(report);
        }
        catch (ArgumentException)
        {
            return Calculation.Failed(ReportErrorCodes.InvalidRequest, ReportErrorCodes.InvalidRequestMessage);
        }

        try
        {
            IReadOnlyList<ReportExpenseLine> lines = await _repository.ListExpenseLinesAsync(query);
            ReportSummary summary = ReportCalculator.Summarize(lines, _timeProvider.GetUtcNow());

            return Calculation.Succeeded(ReportResultSerializer.Serialize(summary), summary.TotalCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ReportStorageException)
        {
            LogCalculationFailed(report.Id, exception.GetType().Name);

            return Calculation.Failed(ReportErrorCodes.ProcessingFailed, ReportErrorCodes.ProcessingFailedMessage);
        }
    }

    private async Task<ReportMessageOutcome> FailAsync(Guid reportId, string errorCode, string errorMessage)
    {
        if (await _store.TryFailAsync(reportId, errorCode, errorMessage, _timeProvider.GetUtcNow()))
        {
            LogFailed(reportId, errorCode);

            return ReportMessageOutcome.PermanentFailure;
        }

        return await ConfirmFinishedAsync(reportId);
    }

    /// <summary>
    /// A conditional write did not apply. It is a duplicate only if the saved state shows the request finished by someone else;
    /// otherwise nothing is saved and the message must come back.
    /// </summary>
    private async Task<ReportMessageOutcome> ConfirmFinishedAsync(Guid reportId)
    {
        ReportRequest? current = await _store.FindAsync(reportId);

        if (current is not null && IsFinished(current.Status))
        {
            LogDuplicate(reportId, current.Status);

            return ReportMessageOutcome.Duplicate;
        }

        LogTransient(reportId, nameof(ReportStatus.Processing));

        return ReportMessageOutcome.TransientFailure;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A message was discarded: it is not a valid report message.")]
    private partial void LogUnreadable();

    [LoggerMessage(Level = LogLevel.Error, Message = "Report {ReportId} was not found, so its message was discarded.")]
    private partial void LogNotFound(Guid reportId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} processing started (resumed: {Resumed}).")]
    private partial void LogStarted(Guid reportId, bool resumed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} completed with {TotalCount} expenses.")]
    private partial void LogCompleted(Guid reportId, int totalCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} is already {Status}: duplicate message ignored, nothing was changed.")]
    private partial void LogDuplicate(Guid reportId, ReportStatus status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Report {ReportId} failed definitively with the code {ErrorCode}.")]
    private partial void LogFailed(Guid reportId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} calculation failed ({ErrorType}).")]
    private partial void LogCalculationFailed(Guid reportId, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} could not be saved now ({ErrorType}): the message will be tried again.")]
    private partial void LogTransient(Guid reportId, string errorType);

    private sealed class Calculation
    {
        private Calculation(string? resultJson, int totalCount, string? errorCode, string? errorMessage)
        {
            ResultJson = resultJson;
            TotalCount = totalCount;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public string? ResultJson { get; }

        public int TotalCount { get; }

        public string? ErrorCode { get; }

        public string? ErrorMessage { get; }

        public static Calculation Succeeded(string resultJson, int totalCount) => new(resultJson, totalCount, null, null);

        public static Calculation Failed(string errorCode, string errorMessage) => new(null, 0, errorCode, errorMessage);
    }
}
