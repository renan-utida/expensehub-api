using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the submission of drafts, using a fake repository and a fixed clock, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServiceSubmitTests
{
    private const string Owner = "owner-1";
    private const string Other = "owner-2";

    /// <summary>The owner submits a draft and it becomes Submitted, with the owner and the content unchanged.</summary>
    [TestMethod]
    public async Task SubmitAsync_OwnerSubmitsDraft_MovesItToSubmitted()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Owner, "Employee"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.AreEqual(Owner, expense.OwnerId);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>The submission is recorded in the history in the same save as the change of state.</summary>
    [TestMethod]
    public async Task SubmitAsync_OwnerSubmitsDraft_RecordsTheSubmissionInTheSameSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Owner, "Employee"), expense.Id);

        Assert.AreEqual(2, repository.HistoryCountAtLastSave);
        ExpenseHistory entry = expense.History.Last();
        Assert.AreEqual(ExpenseHistoryAction.Submitted, entry.Action);
        Assert.AreEqual(Owner, entry.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Draft, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.NewStatus);
        Assert.IsNull(entry.Changes);
    }

    /// <summary>Submitting again is a conflict and writes no second history entry.</summary>
    [TestMethod]
    public async Task SubmitAsync_SubmittingTwice_ReturnsWrongStateAndWritesNoSecondHistory()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        ExpenseService service = NewService(repository);
        ExpenseCaller owner = ExpenseTestData.Caller(Owner, "Employee");

        ExpenseOperationResult first = await service.SubmitAsync(owner, expense.Id);
        ExpenseOperationResult second = await service.SubmitAsync(owner, expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, first.Status);
        Assert.AreEqual(ExpenseOperationStatus.WrongState, second.Status);
        Assert.HasCount(2, expense.History);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>An expense that is not a draft is not submitted, nothing changes and no history is written.</summary>
    /// <param name="status">A state other than Draft.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task SubmitAsync_ExpenseOutsideDraft_ReturnsWrongStateAndChangesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Owner, "Employee"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Another Employee cannot submit the expense, in any state: it exists but is not theirs, so the owner rule answers "forbidden" (an authorization error, not "not found") before the state, and nothing changes.</summary>
    /// <param name="status">The state of the expense of someone else.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task SubmitAsync_AnotherEmployee_ReturnsForbiddenAndChangesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Other, "Employee"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>The submission looks the expense up by its identifier alone and never through the read scope.</summary>
    [TestMethod]
    public async Task SubmitAsync_LooksTheExpenseUpByIdentifierAndNotThroughTheReadScope()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Other, "Employee"), expense.Id);

        Assert.AreEqual(1, repository.FindByIdCalls);
        Assert.AreEqual(0, repository.FindVisibleCalls);
    }

    /// <summary>A user who is Employee and Auditor sees the draft of someone else, so the answer is "not the owner", and nothing changes.</summary>
    [TestMethod]
    public async Task SubmitAsync_EmployeeWhoAlsoAudits_OnAnotherUsersDraft_ReturnsForbidden()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Other, "Employee", "Auditor"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Not being the owner is reported before the state: someone else's submitted expense is "not the owner", not "not a draft".</summary>
    [TestMethod]
    public async Task SubmitAsync_ApproverWhoIsAlsoEmployee_OnAnotherUsersSubmittedExpense_ReturnsForbidden()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Other, "Employee", "Approver"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>The Auditor reads everything but never writes: submitting someone else's draft changes nothing.</summary>
    [TestMethod]
    public async Task SubmitAsync_AuditorAlone_DoesNotGainWriteAccess()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Other, "Auditor"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An Approver alone has no role to submit, so the answer is "forbidden", as the attribute of the endpoint already says.</summary>
    [TestMethod]
    public async Task SubmitAsync_ApproverAlone_OnAnotherUsersDraft_ReturnsForbidden()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Other, "Approver"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>The Admin role alone gives no functional access, not even to an expense the Admin owns.</summary>
    [TestMethod]
    public async Task SubmitAsync_AdminAlone_ReturnsForbidden()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Owner, "Admin"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An expense that does not exist is not found.</summary>
    [TestMethod]
    public async Task SubmitAsync_MissingExpense_ReturnsNotFound()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Owner, "Employee"), Guid.NewGuid());

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>If the state changed between the read and the save (a concurrent submit), the answer is a conflict.</summary>
    [TestMethod]
    public async Task SubmitAsync_StateChangedWhileSaving_ReturnsWrongState()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        repository.ConflictOnNextSave = true;

        ExpenseOperationResult result = await NewService(repository).SubmitAsync(ExpenseTestData.Caller(Owner, "Employee"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>A caller without an identifier cannot submit.</summary>
    /// <param name="userId">A user that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task SubmitAsync_CallerWithoutIdentifier_Throws(string? userId)
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewService(repository).SubmitAsync(ExpenseTestData.Caller(userId!, "Employee"), expense.Id));

        Assert.AreEqual(0, repository.SaveCalls);
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
