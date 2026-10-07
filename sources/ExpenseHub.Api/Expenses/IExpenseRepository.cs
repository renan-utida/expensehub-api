using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Storage operations the expense service needs, kept behind an interface so the rules can be tested without a database.
/// </summary>
public interface IExpenseRepository
{
    /// <summary>
    /// Stores a new expense together with the history entries it carries, in a single save.
    /// </summary>
    /// <param name="expense">The expense, with its history.</param>
    /// <returns>A task that completes when the expense and its history are saved.</returns>
    Task AddAsync(Expense expense);

    /// <summary>
    /// Lists the expenses a user can read, newest first. The scope is applied inside the query, before
    /// any expense is loaded.
    /// </summary>
    /// <param name="scope">What the user is allowed to read.</param>
    /// <returns>The visible expenses; never an expense outside the scope.</returns>
    Task<IReadOnlyList<Expense>> ListAsync(ExpenseScope scope);

    /// <summary>
    /// Finds an expense inside the scope of a user. The scope is part of the query, so an expense outside it
    /// is never loaded and looks the same as one that does not exist.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <param name="scope">What the user is allowed to read.</param>
    /// <returns>The tracked expense, or <c>null</c> when it does not exist or is outside the scope.</returns>
    Task<Expense?> FindVisibleAsync(Guid id, ExpenseScope scope);

    /// <summary>
    /// Finds an expense by its identifier alone, with no read scope. The actions that write (edit, submit, approve, reject
    /// and pay) use it, because for them only an expense that does not exist is "not found": every other wrong situation
    /// is an ownership or state answer decided by the access rule. Reads must keep using <see cref="FindVisibleAsync"/>.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <returns>The tracked expense, or <c>null</c> when it does not exist.</returns>
    Task<Expense?> FindByIdAsync(Guid id);

    /// <summary>
    /// Saves the changes made to expenses returned by this repository, together with the history entries added to them,
    /// in a single save.
    /// </summary>
    /// <exception cref="ExpenseConflictException">
    /// The state of an expense changed since it was read, or a unique index rejected the change because the same change
    /// was saved first (for example, a second payment of the same expense), so nothing was saved.
    /// </exception>
    /// <returns>A task that completes when the changes are saved.</returns>
    Task SaveChangesAsync();
}
