using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Hand-written in-memory fake of <see cref="IExpenseRepository"/>; it never touches a database.
/// Like the real repository, a scoped search only finds an expense inside the scope it is given (the scope is a predicate
/// applied before anything is returned), the search by identifier ignores the scope, and both return the same tracked
/// instance, so the changes the service makes are visible to the tests. It also records how many history entries the expense had when it was saved, to check that the change
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

    /// <summary>Gets how many times an expense was searched inside a read scope.</summary>
    public int FindVisibleCalls { get; private set; }

    /// <summary>Gets how many times an expense was searched by identifier alone.</summary>
    public int FindByIdCalls { get; private set; }

    /// <summary>Gets how many times an expense was searched inside a read scope together with its history.</summary>
    public int FindVisibleWithHistoryCalls { get; private set; }

    /// <summary>Gets how many history entries the added expense carried when it was added.</summary>
    public int HistoryCountWhenAdded { get; private set; }

    /// <summary>Gets how many history entries the edited expense carried at the last save.</summary>
    public int HistoryCountAtLastSave { get; private set; }

    /// <summary>Gets or sets a value indicating whether the next save fails as if the state had changed in the meantime.</summary>
    public bool ConflictOnNextSave { get; set; }

    /// <summary>Gets or sets a value indicating whether the next save fails with an unexpected persistence error.</summary>
    public bool FailOnNextSave { get; set; }

    /// <summary>Gets how many times a save was tried, whether it worked or not.</summary>
    public int SaveAttempts { get; private set; }

    /// <summary>Gets how many expenses had a payment prepared when the last save was tried.</summary>
    public int PaymentsAtLastSaveAttempt { get; private set; }

    /// <summary>Gets how many expenses were in the Paid state when the last save was tried.</summary>
    public int PaidAtLastSaveAttempt { get; private set; }

    /// <summary>Gets how many history entries the expense with most entries had when the last save was tried.</summary>
    public int HistoryCountAtLastSaveAttempt { get; private set; }

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
        FindVisibleCalls++;
        Func<Expense, bool> isVisible = scope.Predicate.Compile();

        return Task.FromResult(_expenses.Where(isVisible).FirstOrDefault(expense => expense.Id == id));
    }

    /// <inheritdoc />
    public Task<Expense?> FindByIdAsync(Guid id)
    {
        FindByIdCalls++;

        return Task.FromResult(_expenses.FirstOrDefault(expense => expense.Id == id));
    }

    /// <inheritdoc />
    public Task<Expense?> FindVisibleWithHistoryAsync(Guid id, ExpenseScope scope)
    {
        FindVisibleWithHistoryCalls++;
        Func<Expense, bool> isVisible = scope.Predicate.Compile();

        return Task.FromResult(_expenses.Where(isVisible).FirstOrDefault(expense => expense.Id == id));
    }

    /// <inheritdoc />
    public Task SaveChangesAsync()
    {
        SaveAttempts++;
        PaymentsAtLastSaveAttempt = _expenses.Count(expense => expense.Payment is not null);
        PaidAtLastSaveAttempt = _expenses.Count(expense => expense.Status == ExpenseStatus.Paid);
        HistoryCountAtLastSaveAttempt = _expenses.Count == 0 ? 0 : _expenses.Max(expense => expense.History.Count);

        if (FailOnNextSave)
        {
            FailOnNextSave = false;
            throw new InvalidOperationException("Simulated persistence failure.");
        }

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
