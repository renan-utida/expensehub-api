using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The possible outcomes of requesting a report.
/// </summary>
public enum ReportOperationStatus
{
    /// <summary>The request was saved and the broker confirmed the message.</summary>
    Accepted,

    /// <summary>The data breaks the validation contract, so nothing was saved and nothing was published.</summary>
    ValidationFailed,

    /// <summary>The request was saved, but the broker did not confirm the message. The request stays <c>Pending</c>.</summary>
    NotPublished,

    /// <summary>The storage failed before the request was saved, so there is no request and no identifier.</summary>
    StorageUnavailable,

    /// <summary>The roles of the user may not request a report, or may not request this scope. Nothing was saved and nothing was published.</summary>
    Forbidden,
}

/// <summary>
/// The result of requesting a report.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Report">The saved request, when the status is <see cref="ReportOperationStatus.Accepted"/> or <see cref="ReportOperationStatus.NotPublished"/>.</param>
/// <param name="Errors">The validation problems, when the status is <see cref="ReportOperationStatus.ValidationFailed"/>.</param>
public sealed record ReportOperationResult(
    ReportOperationStatus Status,
    ReportRequest? Report,
    IReadOnlyList<ReportValidationError> Errors)
{
    /// <summary>Builds the result of a request that was saved and published.</summary>
    /// <param name="report">The saved request.</param>
    /// <returns>The result.</returns>
    public static ReportOperationResult Accept(ReportRequest report) =>
        new(ReportOperationStatus.Accepted, report, Array.Empty<ReportValidationError>());

    /// <summary>Builds the result of a request whose data is invalid.</summary>
    /// <param name="errors">The problems found.</param>
    /// <returns>The result.</returns>
    public static ReportOperationResult Invalid(IReadOnlyList<ReportValidationError> errors) =>
        new(ReportOperationStatus.ValidationFailed, null, errors);

    /// <summary>Builds the result of a request that was saved but not confirmed by the broker.</summary>
    /// <param name="report">The saved request, which stays pending.</param>
    /// <returns>The result.</returns>
    public static ReportOperationResult NotPublished(ReportRequest report) =>
        new(ReportOperationStatus.NotPublished, report, Array.Empty<ReportValidationError>());

    /// <summary>Builds the result of a request that could not be saved.</summary>
    /// <returns>The result.</returns>
    public static ReportOperationResult StorageUnavailable() =>
        new(ReportOperationStatus.StorageUnavailable, null, Array.Empty<ReportValidationError>());

    /// <summary>Builds the result of a request that the roles of the user do not allow.</summary>
    /// <returns>The result.</returns>
    public static ReportOperationResult Forbidden() =>
        new(ReportOperationStatus.Forbidden, null, Array.Empty<ReportValidationError>());
}
