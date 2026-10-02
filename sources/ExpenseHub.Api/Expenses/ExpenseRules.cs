using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The validation contract of an expense, as plain rules that do not depend on EF Core or on the web layer.
/// </summary>
public static class ExpenseRules
{
    /// <summary>Minimum length of the description, after trimming.</summary>
    public const int DescriptionMinLength = 10;

    /// <summary>Maximum length of the description, after trimming.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Smallest accepted amount.</summary>
    public const decimal MinAmount = 0.01m;

    /// <summary>Largest accepted amount, which is <see cref="int.MaxValue"/>.</summary>
    public const decimal MaxAmount = int.MaxValue;

    /// <summary>Maximum number of decimal places of an amount.</summary>
    public const int MaxDecimalPlaces = 2;

    /// <summary>Maximum length of the summary of the changes kept in the history, matching the mapping.</summary>
    public const int ChangesMaxLength = 2000;

    /// <summary>
    /// Checks every field of an expense.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="expenseDate">The date the expense happened.</param>
    /// <param name="today">Today's date, which the expense date cannot be later than.</param>
    /// <returns>The problems found; empty when the data is valid.</returns>
    public static IReadOnlyList<ExpenseValidationError> Validate(
        string? description,
        decimal? amount,
        DateOnly? expenseDate,
        DateOnly today)
    {
        var errors = new List<ExpenseValidationError>();

        string? descriptionError = ValidateDescription(description);
        if (descriptionError is not null)
        {
            errors.Add(new ExpenseValidationError(nameof(ExpenseDetails.Description), descriptionError));
        }

        string? amountError = ValidateAmount(amount);
        if (amountError is not null)
        {
            errors.Add(new ExpenseValidationError(nameof(ExpenseDetails.Amount), amountError));
        }

        string? dateError = ValidateExpenseDate(expenseDate, today);
        if (dateError is not null)
        {
            errors.Add(new ExpenseValidationError(nameof(ExpenseDetails.ExpenseDate), dateError));
        }

        return errors;
    }

    /// <summary>
    /// Checks the description, ignoring spaces at both ends.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The problem found, or <c>null</c> when it is valid.</returns>
    public static string? ValidateDescription(string? description)
    {
        string? trimmed = description?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return "The description is required.";
        }

        if (trimmed.Length < DescriptionMinLength || trimmed.Length > DescriptionMaxLength)
        {
            return $"The description must have between {DescriptionMinLength} and {DescriptionMaxLength} characters.";
        }

        return null;
    }

    /// <summary>
    /// Checks the amount: from 0.01 to <see cref="int.MaxValue"/>, with at most two decimal places.
    /// The amount is never rounded to fit: more than two decimal places is a validation error.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The problem found, or <c>null</c> when it is valid.</returns>
    public static string? ValidateAmount(decimal? amount)
    {
        if (amount is null)
        {
            return "The amount is required.";
        }

        if (amount < MinAmount || amount > MaxAmount)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The amount must be between {MinAmount} and {MaxAmount}.");
        }

        if (decimal.Round(amount.Value, MaxDecimalPlaces) != amount.Value)
        {
            return $"The amount cannot have more than {MaxDecimalPlaces} decimal places.";
        }

        return null;
    }

    /// <summary>
    /// Checks the date of the expense: it must exist and cannot be later than today.
    /// </summary>
    /// <param name="expenseDate">The date the expense happened.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>The problem found, or <c>null</c> when it is valid.</returns>
    public static string? ValidateExpenseDate(DateOnly? expenseDate, DateOnly today)
    {
        if (expenseDate is null)
        {
            return "The expense date is required.";
        }

        return expenseDate.Value > today ? "The expense date cannot be in the future." : null;
    }
}
