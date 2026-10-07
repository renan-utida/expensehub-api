using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the rejection of submitted expenses, using a fake repository and a fixed clock, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServiceRejectTests
{
    private const string Owner = "owner-1";
    private const string Approver = "approver-1";
    private const string ValidReason = "Nota fiscal ilegivel e sem CNPJ";

    /// <summary>An Approver rejects the submitted expense of another user with a reason and it becomes Rejected, with the owner and the content unchanged.</summary>
    [TestMethod]
    public async Task RejectAsync_ApproverRejectsSubmittedExpenseOfAnotherUser_MovesItToRejected()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(Owner, expense.OwnerId);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>The rejection is recorded in the history in the same save as the change of state, with the reason, the actor and the instant from the server.</summary>
    [TestMethod]
    public async Task RejectAsync_ApproverRejects_RecordsTheRejectionAndTheReasonInTheSameSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, ValidReason);

        Assert.AreEqual(2, repository.HistoryCountAtLastSave);
        ExpenseHistory entry = expense.History.Last();
        Assert.AreEqual(ExpenseHistoryAction.Rejected, entry.Action);
        Assert.AreEqual(Approver, entry.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, entry.NewStatus);
        Assert.AreEqual(ValidReason, entry.Reason);
        Assert.IsNull(entry.Changes);
    }

    /// <summary>The reason is trimmed before it is stored.</summary>
    [TestMethod]
    public async Task RejectAsync_ReasonWithSpacesAtTheEnds_StoresItTrimmed()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, "   " + ValidReason + "   ");

        Assert.AreEqual(ValidReason, expense.History.Last().Reason);
    }

    /// <summary>A reason of exactly 10 or 500 characters is accepted and stored whole.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public async Task RejectAsync_ReasonOnTheLimits_IsAcceptedAndStoredWhole(int length)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        string reason = new string('a', length);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, reason);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(reason, expense.History.Last().Reason);
    }

    /// <summary>A missing, empty or blank reason is a validation error on the Reason field, and nothing changes.</summary>
    /// <param name="reason">A reason that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("     ")]
    public async Task RejectAsync_MissingReason_ReturnsValidationErrorAndChangesNothing(string? reason)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, reason);

        AssertReasonRejected(result);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>A reason shorter than 10 or longer than 500 characters is a validation error, and nothing changes.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(9)]
    [DataRow(501)]
    public async Task RejectAsync_ReasonOutsideLimits_ReturnsValidationErrorAndChangesNothing(int length)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, new string('a', length));

        AssertReasonRejected(result);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Spaces do not make a short reason valid: nine characters with spaces around them are still too short.</summary>
    [TestMethod]
    public async Task RejectAsync_NineCharactersPaddedWithSpaces_ReturnsValidationError()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, "   " + new string('a', 9) + "   ");

        AssertReasonRejected(result);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An invalid reason is reported before the expense is looked up, so it comes before "not found".</summary>
    [TestMethod]
    public async Task RejectAsync_InvalidReasonForAMissingExpense_ReportsTheValidationFirst()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), Guid.NewGuid(), "curta");

        AssertReasonRejected(result);
        Assert.AreEqual(0, repository.FindByIdCalls);
    }

    /// <summary>An invalid reason is reported before the owner rule, so it comes before "forbidden" for the owner.</summary>
    [TestMethod]
    public async Task RejectAsync_InvalidReasonOnTheOwnExpense_ReportsTheValidationFirst()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Owner, "Employee", "Approver"), expense.Id, "curta");

        AssertReasonRejected(result);
    }

    /// <summary>An invalid reason is reported before the state, so it comes before "wrong state".</summary>
    [TestMethod]
    public async Task RejectAsync_InvalidReasonForAnApprovedExpense_ReportsTheValidationFirst()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, "curta");

        AssertReasonRejected(result);
    }

    /// <summary>The role is checked before the reason, so a user without it is forbidden even with an invalid reason.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("")]
    public async Task RejectAsync_WithoutTheRole_IsForbiddenBeforeTheReasonIsChecked(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, Roles(roles)), expense.Id, null);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(0, repository.FindByIdCalls);
    }

    /// <summary>Rejecting again is a conflict and writes no second history entry.</summary>
    [TestMethod]
    public async Task RejectAsync_RejectingTwice_ReturnsWrongStateAndWritesNoSecondHistory()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        ExpenseService service = NewService(repository);
        ExpenseCaller approver = ExpenseTestData.Caller(Approver, "Approver");

        ExpenseOperationResult first = await service.RejectAsync(approver, expense.Id, ValidReason);
        ExpenseOperationResult second = await service.RejectAsync(approver, expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, first.Status);
        Assert.AreEqual(ExpenseOperationStatus.WrongState, second.Status);
        Assert.HasCount(2, expense.History);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>A rejected expense cannot be approved: Rejected is a final state.</summary>
    [TestMethod]
    public async Task ApproveAsync_AfterRejection_ReturnsWrongState()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        ExpenseService service = NewService(repository);
        ExpenseCaller approver = ExpenseTestData.Caller(Approver, "Approver");

        await service.RejectAsync(approver, expense.Id, ValidReason);
        ExpenseOperationResult result = await service.ApproveAsync(approver, expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.HasCount(2, expense.History);
    }

    /// <summary>An approved expense cannot be rejected afterwards.</summary>
    [TestMethod]
    public async Task RejectAsync_AfterApproval_ReturnsWrongState()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        ExpenseService service = NewService(repository);
        ExpenseCaller approver = ExpenseTestData.Caller(Approver, "Approver");

        await service.ApproveAsync(approver, expense.Id);
        ExpenseOperationResult result = await service.RejectAsync(approver, expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.HasCount(2, expense.History);
    }

    /// <summary>An expense in any state other than Submitted, a draft included, is a conflict for an Approver who has no other role, and nothing changes.</summary>
    /// <param name="status">A state other than Submitted.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task RejectAsync_ExpenseOutsideSubmitted_ReturnsWrongStateAndChangesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>The rejection looks the expense up by its identifier alone and never through the read scope.</summary>
    [TestMethod]
    public async Task RejectAsync_LooksTheExpenseUpByIdentifierAndNotThroughTheReadScope()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, ValidReason);

        Assert.AreEqual(1, repository.FindByIdCalls);
        Assert.AreEqual(0, repository.FindVisibleCalls);
    }

    /// <summary>Nobody rejects their own expense, whatever the roles and the state: the owner rule comes before the state, and nothing changes.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    /// <param name="status">The state of the expense, which belongs to the user.</param>
    [TestMethod]
    [DataRow("Approver", ExpenseStatus.Submitted)]
    [DataRow("Employee,Approver", ExpenseStatus.Submitted)]
    [DataRow("Employee,Approver", ExpenseStatus.Draft)]
    [DataRow("Approver", ExpenseStatus.Paid)]
    public async Task RejectAsync_ApproverWhoOwnsTheExpense_ReturnsForbiddenAndChangesNothing(string roles, ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Owner, Roles(roles)), expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An expense that does not exist is not found.</summary>
    [TestMethod]
    public async Task RejectAsync_MissingExpense_ReturnsNotFound()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), Guid.NewGuid(), ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Only the Approver role rejects: Employee, Finance, Auditor, Admin and a user without roles are forbidden, and nothing changes.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Finance")]
    [DataRow("Auditor")]
    [DataRow("Admin")]
    [DataRow("Employee,Finance,Auditor,Admin")]
    [DataRow("")]
    public async Task RejectAsync_CallerWithoutTheApproverRole_IsForbiddenAndChangesNothing(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, Roles(roles)), expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Roles accumulate: an Approver who is also Employee, Finance, Auditor or Admin rejects the expense of someone else.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    [TestMethod]
    [DataRow("Employee,Approver")]
    [DataRow("Approver,Finance")]
    [DataRow("Approver,Auditor")]
    [DataRow("Admin,Approver")]
    public async Task RejectAsync_ApproverWithOtherRoles_RejectsAnotherUsersExpense(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, Roles(roles)), expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
    }

    /// <summary>If another decision on the same expense was saved first (a concurrent decision), the answer is a conflict and nothing is saved.</summary>
    [TestMethod]
    public async Task RejectAsync_StateChangedWhileSaving_ReturnsWrongState()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);
        repository.ConflictOnNextSave = true;

        ExpenseOperationResult result = await NewService(repository).RejectAsync(ExpenseTestData.Caller(Approver, "Approver"), expense.Id, ValidReason);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>A caller without an identifier cannot reject.</summary>
    /// <param name="userId">A user that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task RejectAsync_CallerWithoutIdentifier_Throws(string? userId)
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewService(repository).RejectAsync(ExpenseTestData.Caller(userId!, "Approver"), expense.Id, ValidReason));

        Assert.AreEqual(0, repository.SaveCalls);
    }

    private static void AssertReasonRejected(ExpenseOperationResult result)
    {
        Assert.AreEqual(ExpenseOperationStatus.ValidationFailed, result.Status);
        Assert.HasCount(1, result.Errors);
        Assert.AreEqual(nameof(RejectExpenseRequest.Reason), result.Errors[0].Field);
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
