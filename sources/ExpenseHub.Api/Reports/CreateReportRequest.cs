using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Data sent to <c>POST /api/reports</c>. It has only the three fields the client controls. There is no requester, owner,
/// state or instant member on purpose, so the client cannot assign them: a <c>requesterId</c> or an <c>ownerId</c> in the
/// body is ignored, and the requester always comes from the token. All three fields are required, so an omitted date
/// is a <c>400</c> and never the first day of the calendar, and an omitted scope never falls back to <c>Mine</c>.
/// </summary>
public sealed class CreateReportRequest : IValidatableObject
{
    /// <summary>Gets the first day of the period, inclusive, in the form year-month-day.</summary>
    [Required]
    public DateOnly? FromDate { get; init; }

    /// <summary>Gets the last day of the period, inclusive, in the form year-month-day. It cannot be before the first day.</summary>
    [Required]
    public DateOnly? ToDate { get; init; }

    /// <summary>Gets the scope: exactly <c>Mine</c> or <c>Global</c>. A number is not accepted.</summary>
    [Required]
    [ReportScopeName]
    public string? Scope { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        string? error = ReportRules.ValidatePeriodOrder(FromDate, ToDate);

        if (error is not null)
        {
            yield return new ValidationResult(error, new[] { nameof(FromDate) });
        }
    }
}
