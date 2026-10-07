using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Validates the reason of a rejection: from 10 to 500 characters after the spaces at both ends are trimmed, the same
/// rule the service applies. A missing value is left to <see cref="RequiredAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class RejectionReasonAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        string? error = value is string reason
            ? ExpenseRules.ValidateRejectionReason(reason)
            : "The reason must be text.";

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
