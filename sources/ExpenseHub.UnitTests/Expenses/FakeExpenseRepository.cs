using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Hand-written in-memory fake of <see cref="IExpenseRepository"/>; it never touches a database.
/// Like the real repository, it only finds an expense inside the scope it is given (the scope is a predicate applied
/// before anything is returned) and returns the same tracked instance, so the changes the service makes are visible
/// to the tests. It also records how many history entries the expense had when it was saved, to check that the change
/// and its history are saved together, and it can simulate a concurrent change at save time.
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

    /// <summary>Gets how many times the list was asked for.</summary>
    public int ListCalls { get; private set; }

    /// <summary>Gets how many history entries the added expense carried when it was added.</summary>
    public int HistoryCountWhenAdded { get; private set; }

    /// <summary>Gets how many history entries the edited expense carried at the last save.</summary>
    public int HistoryCountAtLastSave { get; private set; }

    /// <summary>Gets or sets a value indicating whether the next save fails as if the state had changed in the meantime.</summary>
    public bool ConflictOnNextSave { get; set; }

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
    public Task<IReadOnlyList<Expense>> ListAsync(ExpenseScope scope)
    {
        ListCalls++;
        Func<Expense, bool> isVisible = scope.Predicate.Compile();

        IReadOnlyList<Expense> visible = _expenses
            .Where(isVisible)
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ThenByDescending(expense => expense.Id)
            .ToList();

        return Task.FromResult(visible);
    }

    /// <inheritdoc />
    public Task<Expense?> FindVisibleAsync(Guid id, ExpenseScope scope)
    {
        Func<Expense, bool> isVisible = scope.Predicate.Compile();

        return Task.FromResult(_expenses.Where(isVisible).FirstOrDefault(expense => expense.Id == id));
    }

    /// <inheritdoc />
    public Task SaveChangesAsync()
    {
        if (ConflictOnNextSave)
        {
            ConflictOnNextSave = false;
            throw new ExpenseConflictException("The expense changed while it was being saved.");
        }

        SaveCalls++;
        HistoryCountAtLastSave = _expenses.Count == 0 ? 0 : _expenses.Max(expense => expense.History.Count);

        return Task.CompletedTask;
    }
}
