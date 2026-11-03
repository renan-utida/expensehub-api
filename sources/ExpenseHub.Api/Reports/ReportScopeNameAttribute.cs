using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Validates the name of a report scope: exactly <c>Mine</c> or <c>Global</c>. A missing value is left to
/// <see cref="RequiredAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ReportScopeNameAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        string? error = value is string text ? ReportRules.ValidateScope(text) : "The scope must be text.";

        if (error is null)
        {
            return ValidationResult.Success;
        }

        // The member name keeps the error attached to the field, with or without the MVC model binder.
        return validationContext.MemberName is null
            ? new ValidationResult(error)
            : new ValidationResult(error, new[] { validationContext.MemberName });
    }
}
