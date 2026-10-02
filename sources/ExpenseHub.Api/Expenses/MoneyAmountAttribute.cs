using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Validates a money amount of an expense: from 0.01 to <see cref="int.MaxValue"/> and with at most two decimal places.
/// A missing value is left to <see cref="RequiredAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MoneyAmountAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        string? error = value is decimal amount
            ? ExpenseRules.ValidateAmount(amount)
            : "The amount must be a decimal number.";

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
