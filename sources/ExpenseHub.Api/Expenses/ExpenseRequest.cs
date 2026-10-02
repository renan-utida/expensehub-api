using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Data sent to <c>POST /api/expenses</c> and, as a full replacement, to <c>PUT /api/expenses/{id}</c>.
/// It has only the three fields the client controls. There is no owner, state, actor or instant member on purpose,
/// so the client cannot assign them (mass assignment): anything else in the body is ignored.
/// All three fields are required, so an omitted field is a <c>400</c> and never "keep the old value".
/// </summary>
public sealed class ExpenseRequest
{
    /// <summary>Gets the description of the expense.</summary>
    [Required]
    [StringLength(ExpenseRules.DescriptionMaxLength, MinimumLength = ExpenseRules.DescriptionMinLength)]
    public string? Description { get; init; }

    /// <summary>Gets the single amount of the expense, with at most two decimal places.</summary>
    [Required]
    [MoneyAmount]
    public decimal? Amount { get; init; }

    /// <summary>Gets the date the expense happened. It cannot be later than today in Brasilia time.</summary>
    [Required]
    public DateOnly? ExpenseDate { get; init; }
}
