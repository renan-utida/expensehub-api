using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the reading of the history of an expense, which has the same visibility as the expense itself,
/// using a fake repository and a fixed clock, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServiceHistoryTests
{
    private const string Owner = "owner-1";
    private const string Reader = "reader-1";
    private const string ValidReason = "Nota fiscal ilegivel e sem CNPJ";

    /// <summary>The owner reads the history of an own expense from the oldest entry to the newest, whatever the order the storage returns.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_OwnerReadsTheOwnHistory_InChronologicalOrder()
    {
        var repository = new FakeExpenseRepository();
        Expense expense = ExpenseTestData.ExpenseOf(Owner, ExpenseStatus.Approved);
        DateTimeOffset start = expense.CreatedAtUtc;
        expense.History.Clear();
        expense.History.Add(Entry(ExpenseHistoryAction.Approved, "approver-1", start.AddHours(2), ExpenseStatus.Submitted, ExpenseStatus.Approved));
        expense.History.Add(Entry(ExpenseHistoryAction.Created, Owner, start, null, ExpenseStatus.Draft));
        expense.History.Add(Entry(ExpenseHistoryAction.Submitted, Owner, start.AddHours(1), ExpenseStatus.Draft, ExpenseStatus.Submitted));
        repository.Given(expense);

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Employee(Owner), expense.Id);

        Assert.IsNotNull(history);
        ExpenseHistoryAction[] actions = history.Select(entry => entry.Action).ToArray();
        CollectionAssert.AreEqual(
            new[] { ExpenseHistoryAction.Created, ExpenseHistoryAction.Submitted, ExpenseHistoryAction.Approved },
            actions);
    }

    /// <summary>The entries keep the actor, the states and the reason, so the owner can read why an expense was rejected.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_OwnerReadsARejectedExpense_SeesTheReasonOfTheRejection()
    {
        var repository = new FakeExpenseRepository();
        Expense expense = ExpenseTestData.ExpenseOf(Owner, ExpenseStatus.Rejected);
        expense.History.Add(Entry(ExpenseHistoryAction.Rejected, "approver-1", expense.CreatedAtUtc.AddHours(1), ExpenseStatus.Submitted, ExpenseStatus.Rejected, ValidReason));
        repository.Given(expense);

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Employee(Owner), expense.Id);

        Assert.IsNotNull(history);
        ExpenseHistory last = history[^1];
        Assert.AreEqual(ExpenseHistoryAction.Rejected, last.Action);
        Assert.AreEqual("approver-1", last.ActorId);
        Assert.AreEqual(ExpenseStatus.Submitted, last.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, last.NewStatus);
        Assert.AreEqual(ValidReason, last.Reason);
    }

    /// <summary>Another Employee cannot read the history of the expense of someone else, in any state: the answer is "not found".</summary>
    /// <param name="status">The state of the expense of someone else.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_AnotherEmployee_ReceivesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Employee(Reader), expense.Id);

        Assert.IsNull(history);
    }

    /// <summary>An Approver reads the history of the expenses it can read, which are the submitted ones.</summary>
    /// <param name="status">The state of the expense.</param>
    /// <param name="visible">Whether the history is readable.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft, false)]
    [DataRow(ExpenseStatus.Submitted, true)]
    [DataRow(ExpenseStatus.Approved, false)]
    [DataRow(ExpenseStatus.Rejected, false)]
    [DataRow(ExpenseStatus.Paid, false)]
    public async Task GetHistoryAsync_Approver_ReadsOnlyTheHistoryOfSubmittedExpenses(ExpenseStatus status, bool visible)
    {
        await AssertVisibilityAsync("Approver", status, visible);
    }

    /// <summary>A Finance user reads the history of the expenses it can read, which are the approved and the paid ones.</summary>
    /// <param name="status">The state of the expense.</param>
    /// <param name="visible">Whether the history is readable.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft, false)]
    [DataRow(ExpenseStatus.Submitted, false)]
    [DataRow(ExpenseStatus.Approved, true)]
    [DataRow(ExpenseStatus.Rejected, false)]
    [DataRow(ExpenseStatus.Paid, true)]
    public async Task GetHistoryAsync_Finance_ReadsOnlyTheHistoryOfApprovedAndPaidExpenses(ExpenseStatus status, bool visible)
    {
        await AssertVisibilityAsync("Finance", status, visible);
    }

    /// <summary>The Auditor reads the history of every expense, in any state.</summary>
    /// <param name="status">The state of the expense.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_Auditor_ReadsTheHistoryOfEveryExpense(ExpenseStatus status)
    {
        await AssertVisibilityAsync("Auditor", status, true);
    }

    /// <summary>Roles accumulate: an Employee who is also an Approver reads the own history and the history of the submitted expenses of others, but not the draft of someone else.</summary>
    /// <param name="mine">Whether the expense belongs to the user.</param>
    /// <param name="status">The state of the expense.</param>
    /// <param name="visible">Whether the history is readable.</param>
    [TestMethod]
    [DataRow(true, ExpenseStatus.Draft, true)]
    [DataRow(false, ExpenseStatus.Submitted, true)]
    [DataRow(false, ExpenseStatus.Draft, false)]
    public async Task GetHistoryAsync_EmployeeWhoIsAlsoApprover_ReadsTheUnionOfTheScopes(bool mine, ExpenseStatus status, bool visible)
    {
        var repository = new FakeExpenseRepository();
        Expense expense = ExpenseTestData.ExpenseOf(mine ? Reader : Owner, status);
        repository.Given(expense);

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Caller(Reader, "Employee", "Approver"), expense.Id);

        Assert.AreEqual(visible, history is not null);
    }

    /// <summary>Admin alone and a user without roles read nothing, and the storage is not even asked.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Admin")]
    [DataRow("")]
    public async Task GetHistoryAsync_AdminAloneOrWithoutRoles_ReceivesNothingWithoutAskingTheStorage(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Caller(Reader, Roles(roles)), expense.Id);

        Assert.IsNull(history);
        Assert.AreEqual(0, repository.FindVisibleWithHistoryCalls);
    }

    /// <summary>An expense that does not exist gives nothing, the same as an expense outside the scope.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_MissingExpense_ReceivesNothing()
    {
        var repository = new FakeExpenseRepository();

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Caller(Reader, "Auditor"), Guid.NewGuid());

        Assert.IsNull(history);
    }

    /// <summary>The history is looked up through the read scope, and never by identifier alone.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_LooksTheExpenseUpThroughTheReadScope()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        await NewService(repository).GetHistoryAsync(ExpenseTestData.Caller(Reader, "Approver"), expense.Id);

        Assert.AreEqual(1, repository.FindVisibleWithHistoryCalls);
        Assert.AreEqual(0, repository.FindByIdCalls);
    }

    /// <summary>A whole life of an expense (create, edit, submit, approve and pay) leaves one entry for each step, with the right actors and states.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_AfterTheWholeLifeOfAnExpense_HasOneEntryForEachStep()
    {
        var repository = new FakeExpenseRepository();
        ExpenseService service = NewService(repository);
        ExpenseCaller owner = ExpenseTestData.Employee(Owner);

        Guid id = (await service.CreateAsync(owner, ExpenseTestData.Valid)).Expense!.Id;
        await service.UpdateAsync(owner, id, new ExpenseDetails("Almoco com cliente em Santos", 90m, ExpenseTestData.Valid.ExpenseDate));
        await service.SubmitAsync(owner, id);
        await service.ApproveAsync(ExpenseTestData.Caller("approver-1", "Approver"), id);
        await service.PayAsync(ExpenseTestData.Caller("finance-1", "Finance"), id);

        IReadOnlyList<ExpenseHistory>? history = await service.GetHistoryAsync(ExpenseTestData.Caller(Reader, "Auditor"), id);

        Assert.IsNotNull(history);
        CollectionAssert.AreEqual(
            new[]
            {
                ExpenseHistoryAction.Created,
                ExpenseHistoryAction.Edited,
                ExpenseHistoryAction.Submitted,
                ExpenseHistoryAction.Approved,
                ExpenseHistoryAction.Paid,
            },
            history.Select(entry => entry.Action).ToArray());
        CollectionAssert.AreEqual(
            new[] { Owner, Owner, Owner, "approver-1", "finance-1" },
            history.Select(entry => entry.ActorId).ToArray());
        Assert.IsNull(history[0].PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Approved, history[4].PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, history[4].NewStatus);
    }

    /// <summary>The reason of a rejection reaches the history that the owner reads.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_AfterARejection_ShowsTheReasonToTheOwner()
    {
        var repository = new FakeExpenseRepository();
        ExpenseService service = NewService(repository);
        ExpenseCaller owner = ExpenseTestData.Employee(Owner);

        Guid id = (await service.CreateAsync(owner, ExpenseTestData.Valid)).Expense!.Id;
        await service.SubmitAsync(owner, id);
        await service.RejectAsync(ExpenseTestData.Caller("approver-1", "Approver"), id, "   " + ValidReason + "   ");

        IReadOnlyList<ExpenseHistory>? history = await service.GetHistoryAsync(owner, id);

        Assert.IsNotNull(history);
        Assert.AreEqual(ValidReason, history[^1].Reason);
    }

    /// <summary>A missing caller cannot read a history.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_NullCaller_Throws()
    {
        var repository = new FakeExpenseRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => NewService(repository).GetHistoryAsync(null!, Guid.NewGuid()));
    }

    private static async Task AssertVisibilityAsync(string roles, ExpenseStatus status, bool visible)
    {
        var (repository, expense) = Given(status);

        IReadOnlyList<ExpenseHistory>? history = await NewService(repository).GetHistoryAsync(ExpenseTestData.Caller(Reader, Roles(roles)), expense.Id);

        Assert.AreEqual(visible, history is not null);

        if (visible)
        {
            Assert.IsNotEmpty(history!);
        }
    }

    private static ExpenseHistory Entry(
        ExpenseHistoryAction action,
        string actorId,
        DateTimeOffset at,
        ExpenseStatus? previous,
        ExpenseStatus next,
        string? reason = null)
    {
        return new ExpenseHistory
        {
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = at,
            PreviousStatus = previous,
            NewStatus = next,
            Reason = reason,
        };
    }

    private static string[] Roles(string roles)
    {
        return roles.Length == 0 ? [] : roles.Split(',');
    }

    private static (FakeExpenseRepository Repository, Expense Expense) Given(ExpenseStatus status)
    {
        var repository = new FakeExpenseRepository();
        Expense expense = ExpenseTestData.ExpenseOf(Owner, status);
        repository.Given(expense);

        return (repository, expense);
    }

    private static ExpenseService NewService(FakeExpenseRepository repository)
    {
        return new ExpenseService(repository, new FixedTimeProvider(ExpenseTestData.Now));
    }
}
