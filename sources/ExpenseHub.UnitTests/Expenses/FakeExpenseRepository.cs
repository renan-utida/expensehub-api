using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Hand-written in-memory fake of <see cref="IExpenseRepository"/>; it never touches a database.
/// Like the real repository, it only finds an expense for its owner and returns the same tracked instance,
/// so the changes the service makes are visible to the tests. It also records how many history entries
/// the expense had when it was saved, to check that the change and its history are saved together.
/// </summary>
internal sealed class FakeExpenseRepository : IExpenseRepository
{
    private readonly List<Expense> _expenses = new();

    /// <summary>Gets the expenses in the repository.</summary>
    public IReadOnlyList<Expense> Expenses => _expenses;

    /// <summary>Gets how many times an expense was added.</summary>
    public int AddCalls { get; private set; }

    /// <summary>Gets how many times the changes were saved.</summary>
    public int SaveCalls { get; private set; }

    /// <summary>Gets how many history entries the added expense carried when it was added.</summary>
    public int HistoryCountWhenAdded { get; private set; }

    /// <summary>Gets how many history entries the edited expense carried at the last save.</summary>
    public int HistoryCountAtLastSave { get; private set; }

    /// <summary>Adds an expense directly, without counting it as an addition made by the service.</summary>
    /// <param name="expense">The expense.</param>
    public void Given(Expense expense)
    {
        _expenses.Add(expense);
    }

    /// <inheritdoc />
    public Task AddAsync(Expense expense)
    {
        AddCalls++;
        HistoryCountWhenAdded = expense.History.Count;
        _expenses.Add(expense);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Expense?> FindOwnedAsync(Guid id, string ownerId)
    {
        return Task.FromResult(_expenses.FirstOrDefault(expense => expense.Id == id && expense.OwnerId == ownerId));
    }

    /// <inheritdoc />
    public Task SaveChangesAsync()
    {
        SaveCalls++;
        HistoryCountAtLastSave = _expenses.Count == 0 ? 0 : _expenses.Max(expense => expense.History.Count);

        return Task.CompletedTask;
    }
}
