using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The possible outcomes of creating or editing an expense.
/// </summary>
public enum ExpenseOperationStatus
{
    /// <summary>The operation was done and saved.</summary>
    Succeeded,

    /// <summary>The data breaks the validation contract and nothing was saved.</summary>
    ValidationFailed,

    /// <summary>The expense does not exist or is not visible to the user.</summary>
    NotFound,

    /// <summary>The state of the expense does not accept the action (for example, a repeated submission), or it changed while the action was being saved.</summary>
    WrongState,

    /// <summary>The user lacks the role of the action, or the ownership rule forbids it (for example, editing the expense of someone else).</summary>
    Forbidden,
}

/// <summary>
/// The result of creating or editing an expense.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Expense">The expense, when the status is <see cref="ExpenseOperationStatus.Succeeded"/>.</param>
/// <param name="Errors">The validation problems, when the status is <see cref="ExpenseOperationStatus.ValidationFailed"/>.</param>
public sealed record ExpenseOperationResult(
    ExpenseOperationStatus Status,
    Expense? Expense,
    IReadOnlyList<ExpenseValidationError> Errors)
{
    /// <summary>Builds the result of an operation that worked.</summary>
    /// <param name="expense">The expense.</param>
    /// <returns>The result.</returns>
    public static ExpenseOperationResult Success(Expense expense) =>
        new(ExpenseOperationStatus.Succeeded, expense, Array.Empty<ExpenseValidationError>());

    /// <summary>Builds the result of an operation whose data is invalid.</summary>
    /// <param name="errors">The problems found.</param>
    /// <returns>The result.</returns>
    public static ExpenseOperationResult Invalid(IReadOnlyList<ExpenseValidationError> errors) =>
        new(ExpenseOperationStatus.ValidationFailed, null, errors);

    /// <summary>Builds the result for an expense that does not exist or is not visible.</summary>
    /// <returns>The result.</returns>
    public static ExpenseOperationResult NotFound() =>
        new(ExpenseOperationStatus.NotFound, null, Array.Empty<ExpenseValidationError>());

    /// <summary>Builds the result for an expense whose state does not accept the action.</summary>
    /// <returns>The result.</returns>
    public static ExpenseOperationResult WrongState() =>
        new(ExpenseOperationStatus.WrongState, null, Array.Empty<ExpenseValidationError>());

    /// <summary>Builds the result for an action the user is not allowed to do (no role, or the ownership rule forbids it).</summary>
    /// <returns>The result.</returns>
    public static ExpenseOperationResult Forbidden() =>
        new(ExpenseOperationStatus.Forbidden, null, Array.Empty<ExpenseValidationError>());
}
