using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The state of the report requests as the worker needs it. Every change is a conditional transition that applies only if the
/// request is still in the state it was read in, which makes the processing idempotent without any memory in the worker: whatever
/// is saved here survives a restart. A transition that does not apply returns <c>false</c> and changes nothing. When the storage
/// is temporarily unavailable, every method throws <see cref="ReportStorageException"/>, so a failure to save is never mistaken
/// for a saved state.
/// </summary>
public interface IReportProcessingStore
{
    /// <summary>
    /// Reads the saved request without tracking it. The scope, the requester and the period of the request are what the worker
    /// uses; nothing comes from the message.
    /// </summary>
    /// <param name="reportId">The identifier of the request.</param>
    /// <returns>The request, or <c>null</c> when there is none.</returns>
    /// <exception cref="ReportStorageException">The storage is temporarily unavailable.</exception>
    Task<ReportRequest?> FindAsync(Guid reportId);

    /// <summary>
    /// Moves a <c>Pending</c> request to <c>Processing</c> and records when the processing started.
    /// </summary>
    /// <param name="reportId">The identifier of the request.</param>
    /// <param name="startedAtUtc">The UTC instant the processing started.</param>
    /// <returns><c>true</c> when the request was <c>Pending</c> and now is <c>Processing</c>.</returns>
    /// <exception cref="ReportStorageException">The storage is temporarily unavailable.</exception>
    Task<bool> TryStartAsync(Guid reportId, DateTimeOffset startedAtUtc);

    /// <summary>
    /// Moves a <c>Processing</c> request to <c>Completed</c>, and saves the instant and the result in the same write, so a
    /// completed request always has its result and a result is never saved without the completion.
    /// </summary>
    /// <param name="reportId">The identifier of the request.</param>
    /// <param name="resultJson">The result, written by <see cref="ReportResultSerializer"/>.</param>
    /// <param name="completedAtUtc">The UTC instant the request completed.</param>
    /// <returns><c>true</c> when the request was <c>Processing</c> and now is <c>Completed</c>.</returns>
    /// <exception cref="ReportStorageException">The storage is temporarily unavailable.</exception>
    Task<bool> TryCompleteAsync(Guid reportId, string resultJson, DateTimeOffset completedAtUtc);

    /// <summary>
    /// Moves a <c>Pending</c> or <c>Processing</c> request to <c>Failed</c>, with a stable code and a sanitized message.
    /// </summary>
    /// <param name="reportId">The identifier of the request.</param>
    /// <param name="errorCode">The stable code of the error.</param>
    /// <param name="errorMessage">The sanitized message of the error.</param>
    /// <param name="completedAtUtc">The UTC instant the request failed.</param>
    /// <returns><c>true</c> when the request was not finished and now is <c>Failed</c>.</returns>
    /// <exception cref="ReportStorageException">The storage is temporarily unavailable.</exception>
    Task<bool> TryFailAsync(Guid reportId, string errorCode, string errorMessage, DateTimeOffset completedAtUtc);
}
