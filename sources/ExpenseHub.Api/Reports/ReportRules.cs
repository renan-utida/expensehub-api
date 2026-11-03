using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The validation contract of a report request: the scope is exactly <c>Mine</c> or <c>Global</c>, both dates are present, and the
/// period does not end before it starts. They are plain functions, used by the DTO and by the service, so one rule is
/// written once and tested without a host.
/// </summary>
public static class ReportRules
{
    /// <summary>The name of the field of the first day of the period.</summary>
    public const string FromDateField = "FromDate";

    /// <summary>The name of the field of the last day of the period.</summary>
    public const string ToDateField = "ToDate";

    /// <summary>The name of the field of the scope.</summary>
    public const string ScopeField = "Scope";

    /// <summary>
    /// Reads the scope from its name. Only the exact names <c>Mine</c> and <c>Global</c> are accepted: a number, a number in
    /// quotes, a name in other letter case or an empty text is not a scope. It never uses <c>Enum.TryParse</c>, which would accept numbers.
    /// </summary>
    /// <param name="text">The text sent by the client.</param>
    /// <param name="scope">The scope, when the text is valid.</param>
    /// <returns><c>true</c> when the text is the name of a scope.</returns>
    public static bool TryParseScope(string? text, out ReportScope scope)
    {
        switch (text)
        {
            case nameof(ReportScope.Mine):
                scope = ReportScope.Mine;
                return true;
            case nameof(ReportScope.Global):
                scope = ReportScope.Global;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    /// <summary>
    /// Checks the scope text.
    /// </summary>
    /// <param name="text">The text sent by the client.</param>
    /// <returns>The message of the problem, or <c>null</c> when the scope is valid.</returns>
    public static string? ValidateScope(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "The scope is required.";
        }

        return TryParseScope(text, out _) ? null : "The scope must be Mine or Global.";
    }

    /// <summary>
    /// Checks that the period does not end before it starts. A start equal to the end is a valid period of one day.
    /// A missing date is left to the check of the required fields.
    /// </summary>
    /// <param name="fromDate">The first day of the period.</param>
    /// <param name="toDate">The last day of the period.</param>
    /// <returns>The message of the problem, or <c>null</c> when the order is valid or a date is missing.</returns>
    public static string? ValidatePeriodOrder(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return "The start date cannot be after the end date.";
        }

        return null;
    }

    /// <summary>
    /// Checks every field of a report request.
    /// </summary>
    /// <param name="details">The fields chosen by the client.</param>
    /// <returns>The problems found; empty when the data is valid.</returns>
    public static IReadOnlyList<ReportValidationError> Validate(ReportRequestDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var errors = new List<ReportValidationError>();

        if (details.FromDate is null)
        {
            errors.Add(new ReportValidationError(FromDateField, "The start date is required."));
        }

        if (details.ToDate is null)
        {
            errors.Add(new ReportValidationError(ToDateField, "The end date is required."));
        }

        string? scopeError = ValidateScope(details.Scope);

        if (scopeError is not null)
        {
            errors.Add(new ReportValidationError(ScopeField, scopeError));
        }

        string? periodError = ValidatePeriodOrder(details.FromDate, details.ToDate);

        if (periodError is not null)
        {
            errors.Add(new ReportValidationError(FromDateField, periodError));
        }

        return errors;
    }
}
