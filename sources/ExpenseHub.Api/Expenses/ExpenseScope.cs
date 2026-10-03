using System;
using System.Linq.Expressions;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// What a user is allowed to read. The scope is a predicate, so the storage applies it inside the query,
/// before any expense is loaded, and never loads everything to filter afterwards.
/// </summary>
public sealed class ExpenseScope
{
    private readonly Lazy<Func<Expense, bool>> _compiled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpenseScope"/> class.
    /// </summary>
    /// <param name="ownerId">The user whose own expenses are visible, or <c>null</c> when own expenses give no access.</param>
    /// <param name="seesSubmitted">Whether the expenses in <c>Submitted</c> state of any owner are visible.</param>
    /// <param name="seesApprovedAndPaid">Whether the expenses in <c>Approved</c> and <c>Paid</c> states of any owner are visible.</param>
    /// <param name="seesAll">Whether every expense is visible.</param>
    public ExpenseScope(string? ownerId, bool seesSubmitted, bool seesApprovedAndPaid, bool seesAll)
    {
        OwnerId = ownerId;
        SeesSubmitted = seesSubmitted;
        SeesApprovedAndPaid = seesApprovedAndPaid;
        SeesAll = seesAll;
        Predicate = BuildPredicate(ownerId, seesSubmitted, seesApprovedAndPaid, seesAll);
        _compiled = new Lazy<Func<Expense, bool>>(() => Predicate.Compile());
    }

    /// <summary>Gets the user whose own expenses are visible, or <c>null</c>.</summary>
    public string? OwnerId { get; }

    /// <summary>
    /// Checks one expense that is already in memory against the scope, with the same condition the storage applies in the query.
    /// This is used to decide an action on an expense that was loaded; it is never used to filter a list.
    /// </summary>
    /// <param name="expense">The expense.</param>
    /// <returns><c>true</c> when the expense is inside the scope.</returns>
    public bool Allows(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return _compiled.Value(expense);
    }

    /// <summary>Gets a value indicating whether the expenses in <c>Submitted</c> state are visible.</summary>
    public bool SeesSubmitted { get; }

    /// <summary>Gets a value indicating whether the expenses in <c>Approved</c> and <c>Paid</c> states are visible.</summary>
    public bool SeesApprovedAndPaid { get; }

    /// <summary>Gets a value indicating whether every expense is visible.</summary>
    public bool SeesAll { get; }

    /// <summary>Gets a value indicating whether the scope sees nothing at all.</summary>
    public bool IsEmpty => !SeesAll && OwnerId is null && !SeesSubmitted && !SeesApprovedAndPaid;

    /// <summary>
    /// Gets the condition an expense must meet to be visible: the union of every part of the scope.
    /// The values are captured, not written into the expression, so the query is translated once for every user.
    /// </summary>
    public Expression<Func<Expense, bool>> Predicate { get; }

    private static Expression<Func<Expense, bool>> BuildPredicate(
        string? ownerId,
        bool seesSubmitted,
        bool seesApprovedAndPaid,
        bool seesAll)
    {
        return expense =>
            seesAll
            || (ownerId != null && expense.OwnerId == ownerId)
            || (seesSubmitted && expense.Status == ExpenseStatus.Submitted)
            || (seesApprovedAndPaid && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid));
    }
}
