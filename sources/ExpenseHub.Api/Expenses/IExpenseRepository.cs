using System;
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
    /// Finds an expense that belongs to a user. The owner filter is part of the query, so an expense of
    /// someone else is never loaded and looks the same as one that does not exist.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <param name="ownerId">The identifier of the owner.</param>
    /// <returns>The tracked expense, or <c>null</c> when it does not exist or belongs to another user.</returns>
    Task<Expense?> FindOwnedAsync(Guid id, string ownerId);

    /// <summary>
    /// Saves the changes made to expenses returned by this repository, together with the history entries added to them,
    /// in a single save.
    /// </summary>
    /// <returns>A task that completes when the changes are saved.</returns>
    Task SaveChangesAsync();
}
