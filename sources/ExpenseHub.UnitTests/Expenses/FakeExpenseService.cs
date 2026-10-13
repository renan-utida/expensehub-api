using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Hand-written fake of <see cref="ExpenseService"/> for the controller tests. It runs none of the rules of the service:
/// every method records what the controller passed and answers with the outcome the test chose, so a controller test checks
/// only what belongs to the controller. The rules of the service have their own tests.
/// </summary>
internal sealed class FakeExpenseService : ExpenseService
{
    private readonly List<string> _calls = new();

    /// <summary>Initializes a new instance of the <see cref="FakeExpenseService"/> class, which never reaches its repository.</summary>
    public FakeExpenseService()
        : base(new FakeExpenseRepository(), new FixedTimeProvider(ExpenseTestData.Now))
    {
    }

    /// <summary>Gets or sets the outcome of create, edit, submit, approve, reject and pay.</summary>
    public ExpenseOperationResult Result { get; set; } = ExpenseOperationResult.NotFound();

    /// <summary>Gets or sets the expenses the list answers with.</summary>
    public IReadOnlyList<Expense> ListResult { get; set; } = Array.Empty<Expense>();

    /// <summary>Gets or sets the expense the detail answers with, or <c>null</c> for one that cannot be read.</summary>
    public Expense? GetResult { get; set; }

    /// <summary>Gets or sets the entries the history answers with, or <c>null</c> for an expense that cannot be read.</summary>
    public IReadOnlyList<ExpenseHistory>? HistoryResult { get; set; }

    /// <summary>Gets the names of the methods the controller called, in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Gets the user the controller passed in the last call.</summary>
    public ExpenseCaller? LastCaller { get; private set; }

    /// <summary>Gets the expense identifier the controller passed in the last call.</summary>
    public Guid? LastExpenseId { get; private set; }

    /// <summary>Gets the fields the controller passed in the last create or edit.</summary>
    public ExpenseDetails? LastDetails { get; private set; }

    /// <summary>Gets the reason the controller passed in the last rejection.</summary>
    public string? LastReason { get; private set; }

    /// <inheritdoc />
    public override Task<ExpenseOperationResult> CreateAsync(ExpenseCaller caller, ExpenseDetails details)
    {
        Record(nameof(CreateAsync), caller, null, details, null);

        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public override Task<ExpenseOperationResult> UpdateAsync(ExpenseCaller caller, Guid expenseId, ExpenseDetails details)
    {
        Record(nameof(UpdateAsync), caller, expenseId, details, null);

        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public override Task<ExpenseOperationResult> SubmitAsync(ExpenseCaller caller, Guid expenseId)
    {
        Record(nameof(SubmitAsync), caller, expenseId, null, null);

        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public override Task<ExpenseOperationResult> ApproveAsync(ExpenseCaller caller, Guid expenseId)
    {
        Record(nameof(ApproveAsync), caller, expenseId, null, null);

        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public override Task<ExpenseOperationResult> RejectAsync(ExpenseCaller caller, Guid expenseId, string? reason)
    {
        Record(nameof(RejectAsync), caller, expenseId, null, reason);

        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public override Task<ExpenseOperationResult> PayAsync(ExpenseCaller caller, Guid expenseId)
    {
        Record(nameof(PayAsync), caller, expenseId, null, null);

        return Task.FromResult(Result);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<Expense>> ListAsync(ExpenseCaller caller)
    {
        Record(nameof(ListAsync), caller, null, null, null);

        return Task.FromResult(ListResult);
    }

    /// <inheritdoc />
    public override Task<Expense?> GetAsync(ExpenseCaller caller, Guid expenseId)
    {
        Record(nameof(GetAsync), caller, expenseId, null, null);

        return Task.FromResult(GetResult);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<ExpenseHistory>?> GetHistoryAsync(ExpenseCaller caller, Guid expenseId)
    {
        Record(nameof(GetHistoryAsync), caller, expenseId, null, null);

        return Task.FromResult(HistoryResult);
    }

    private void Record(string method, ExpenseCaller caller, Guid? expenseId, ExpenseDetails? details, string? reason)
    {
        _calls.Add(method);
        LastCaller = caller;
        LastExpenseId = expenseId;
        LastDetails = details;
        LastReason = reason;
    }
}
