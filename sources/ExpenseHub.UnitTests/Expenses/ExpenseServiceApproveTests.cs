using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the approval of submitted expenses, using a fake repository and a fixed clock, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServiceApproveTests
{
    private const string Owner = "owner-1";
    private const string Approver = "approver-1";

    /// <summary>An Approver approves the submitted expense of another user and it becomes Approved, with the owner and the content unchanged.</summary>
    [TestMethod]
    public async Task ApproveAsync_ApproverApprovesSubmittedExpenseOfAnotherUser_MovesItToApproved()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.AreEqual(Owner, expense.OwnerId);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>The approval is recorded in the history in the same save as the change of state, with the actor and the instant from the server.</summary>
    [TestMethod]
    public async Task ApproveAsync_ApproverApproves_RecordsTheApprovalInTheSameSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id);

        Assert.AreEqual(2, repository.HistoryCountAtLastSave);
        ExpenseHistory entry = expense.History.Last();
        Assert.AreEqual(ExpenseHistoryAction.Approved, entry.Action);
        Assert.AreEqual(Approver, entry.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Approved, entry.NewStatus);
        Assert.IsNull(entry.Reason);
        Assert.IsNull(entry.Changes);
    }

    /// <summary>Approving again is a conflict and writes no second history entry.</summary>
    [TestMethod]
    public async Task ApproveAsync_ApprovingTwice_ReturnsWrongStateAndWritesNoSecondHistory()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        ExpenseService service = NewService(repository);
        ExpenseCaller approver = ExpenseTestData.Caller(Approver, "Approver");

        ExpenseOperationResult first = await service.ApproveAsync(approver, expense.Id);
        ExpenseOperationResult second = await service.ApproveAsync(approver, expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, first.Status);
        Assert.AreEqual(ExpenseOperationStatus.WrongState, second.Status);
        Assert.HasCount(2, expense.History);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>An expense in any state other than Submitted, a draft included, is a conflict for an Approver who has no other role, and nothing changes.</summary>
    /// <param name="status">A state other than Submitted.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task ApproveAsync_ExpenseOutsideSubmitted_ReturnsWrongStateAndChangesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>The approval looks the expense up by its identifier alone and never through the read scope.</summary>
    [TestMethod]
    public async Task ApproveAsync_LooksTheExpenseUpByIdentifierAndNotThroughTheReadScope()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id);

        Assert.AreEqual(1, repository.FindByIdCalls);
        Assert.AreEqual(0, repository.FindVisibleCalls);
    }

    /// <summary>Nobody approves their own expense, whatever the roles and the state: the owner rule comes before the state, and nothing changes.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    /// <param name="status">The state of the expense, which belongs to the user.</param>
    [TestMethod]
    [DataRow("Approver", ExpenseStatus.Submitted)]
    [DataRow("Employee,Approver", ExpenseStatus.Submitted)]
    [DataRow("Employee,Approver,Finance", ExpenseStatus.Submitted)]
    [DataRow("Approver", ExpenseStatus.Draft)]
    [DataRow("Employee,Approver", ExpenseStatus.Draft)]
    [DataRow("Approver", ExpenseStatus.Approved)]
    public async Task ApproveAsync_ApproverWhoOwnsTheExpense_ReturnsForbiddenAndChangesNothing(string roles, ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Owner, Roles(roles)), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An expense that does not exist is not found.</summary>
    [TestMethod]
    public async Task ApproveAsync_MissingExpense_ReturnsNotFound()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Approver"), Guid.NewGuid());

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Only the Approver role approves: Employee, Finance, Auditor, Admin and a user without roles are forbidden, and nothing changes.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Finance")]
    [DataRow("Auditor")]
    [DataRow("Admin")]
    [DataRow("Employee,Finance,Auditor,Admin")]
    [DataRow("")]
    public async Task ApproveAsync_CallerWithoutTheApproverRole_IsForbiddenAndChangesNothing(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, Roles(roles)), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>The role is checked first, so a user without it is forbidden even for an expense that does not exist.</summary>
    [TestMethod]
    public async Task ApproveAsync_WithoutTheRole_IsForbiddenEvenForAMissingExpense()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Employee"), Guid.NewGuid());

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(0, repository.FindByIdCalls);
    }

    /// <summary>Roles accumulate: an Approver who is also Employee, Finance, Auditor or Admin approves the expense of someone else.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    [TestMethod]
    [DataRow("Employee,Approver")]
    [DataRow("Approver,Finance")]
    [DataRow("Approver,Auditor")]
    [DataRow("Admin,Approver")]
    public async Task ApproveAsync_ApproverWithOtherRoles_ApprovesAnotherUsersExpense(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, Roles(roles)), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
    }

    /// <summary>If another decision on the same expense was saved first (a concurrent approval), the answer is a conflict and nothing is saved.</summary>
    [TestMethod]
    public async Task ApproveAsync_StateChangedWhileSaving_ReturnsWrongState()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        repository.ConflictOnNextSave = true;

        ExpenseOperationResult result = await NewService(repository).ApproveAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>A caller without an identifier cannot approve.</summary>
    /// <param name="userId">A user that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task ApproveAsync_CallerWithoutIdentifier_Throws(string? userId)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewService(repository).ApproveAsync(ExpenseTestData.Caller(userId!, "Approver"), expense.Id));

        Assert.AreEqual(0, repository.SaveCalls);
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
