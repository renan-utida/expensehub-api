using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the payment of approved expenses, using a fake repository and a fixed clock, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServicePayTests
{
    private const string Owner = "owner-1";
    private const string Payer = "finance-1";

    /// <summary>A Finance user pays the approved expense of another user and it becomes Paid, with the owner and the content unchanged.</summary>
    [TestMethod]
    public async Task PayAsync_FinancePaysApprovedExpenseOfAnotherUser_MovesItToPaid()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
        Assert.AreEqual(Owner, expense.OwnerId);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>The payment record keeps who paid, taken from the token, and when, taken from the server clock.</summary>
    [TestMethod]
    public async Task PayAsync_FinancePays_RecordsThePaymentWithTheActorAndTheServerInstant()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.IsNotNull(expense.Payment);
        Assert.AreEqual(Payer, expense.Payment.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, expense.Payment.PaidAtUtc);
    }

    /// <summary>The payment is recorded in the history in the same save as the change of state, with the actor and the instant from the server.</summary>
    [TestMethod]
    public async Task PayAsync_FinancePays_RecordsThePaymentInTheHistoryInTheSameSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.AreEqual(2, repository.HistoryCountAtLastSave);
        ExpenseHistory entry = expense.History.Last();
        Assert.AreEqual(ExpenseHistoryAction.Paid, entry.Action);
        Assert.AreEqual(Payer, entry.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Approved, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, entry.NewStatus);
        Assert.IsNull(entry.Reason);
        Assert.IsNull(entry.Changes);
    }

    /// <summary>The new state, the payment record and the history entry are all prepared before one single save, so none is saved apart from the others.</summary>
    [TestMethod]
    public async Task PayAsync_FinancePays_PreparesTheStateThePaymentAndTheHistoryForASingleSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.AreEqual(1, repository.SaveAttempts);
        Assert.AreEqual(1, repository.PaidAtLastSaveAttempt);
        Assert.AreEqual(1, repository.PaymentsAtLastSaveAttempt);
        Assert.AreEqual(2, repository.HistoryCountAtLastSaveAttempt);
    }

    /// <summary>Paying again is a conflict: no second payment record and no second history entry.</summary>
    [TestMethod]
    public async Task PayAsync_PayingTwice_ReturnsWrongStateAndWritesNoSecondPaymentOrHistory()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);
        ExpenseService service = NewService(repository);
        ExpenseCaller finance = ExpenseTestData.Caller(Payer, "Finance");

        ExpenseOperationResult first = await service.PayAsync(finance, expense.Id);
        PaymentRecord? payment = expense.Payment;
        ExpenseOperationResult second = await service.PayAsync(finance, expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, first.Status);
        Assert.AreEqual(ExpenseOperationStatus.WrongState, second.Status);
        Assert.AreSame(payment, expense.Payment);
        Assert.HasCount(2, expense.History);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>An expense in any state other than Approved is a conflict, a draft included, and nothing changes.</summary>
    /// <param name="status">A state other than Approved.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task PayAsync_ExpenseOutsideApproved_ReturnsWrongStateAndChangesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.IsNull(expense.Payment);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveAttempts);
    }

    /// <summary>The payment looks the expense up by its identifier alone and never through the read scope.</summary>
    [TestMethod]
    public async Task PayAsync_LooksTheExpenseUpByIdentifierAndNotThroughTheReadScope()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.AreEqual(1, repository.FindByIdCalls);
        Assert.AreEqual(0, repository.FindVisibleCalls);
    }

    /// <summary>Nobody pays their own expense, whatever the roles and the state: the owner rule comes before the state, and nothing changes.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    /// <param name="status">The state of the expense, which belongs to the user.</param>
    [TestMethod]
    [DataRow("Finance", ExpenseStatus.Approved)]
    [DataRow("Employee,Finance", ExpenseStatus.Approved)]
    [DataRow("Employee,Approver,Finance", ExpenseStatus.Approved)]
    [DataRow("Finance", ExpenseStatus.Draft)]
    [DataRow("Finance", ExpenseStatus.Paid)]
    public async Task PayAsync_FinanceWhoOwnsTheExpense_ReturnsForbiddenAndChangesNothing(string roles, ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Owner, Roles(roles)), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(status, expense.Status);
        Assert.IsNull(expense.Payment);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveAttempts);
    }

    /// <summary>An expense that does not exist is not found.</summary>
    [TestMethod]
    public async Task PayAsync_MissingExpense_ReturnsNotFound()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), Guid.NewGuid());

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
        Assert.AreEqual(0, repository.SaveAttempts);
    }

    /// <summary>Only the Finance role pays: Employee, Approver, Auditor, Admin and a user without roles are forbidden, and nothing changes.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Approver")]
    [DataRow("Auditor")]
    [DataRow("Admin")]
    [DataRow("Employee,Approver,Auditor,Admin")]
    [DataRow("")]
    public async Task PayAsync_CallerWithoutTheFinanceRole_IsForbiddenAndChangesNothing(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, Roles(roles)), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.IsNull(expense.Payment);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveAttempts);
    }

    /// <summary>The role is checked first, so a user without it is forbidden even for an expense that does not exist.</summary>
    [TestMethod]
    public async Task PayAsync_WithoutTheRole_IsForbiddenEvenForAMissingExpense()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Approver"), Guid.NewGuid());

        Assert.AreEqual(ExpenseOperationStatus.Forbidden, result.Status);
        Assert.AreEqual(0, repository.FindByIdCalls);
    }

    /// <summary>Roles accumulate: a Finance user who is also Employee, Approver, Auditor or Admin pays the expense of someone else.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    [TestMethod]
    [DataRow("Employee,Finance")]
    [DataRow("Approver,Finance")]
    [DataRow("Auditor,Finance")]
    [DataRow("Admin,Finance")]
    public async Task PayAsync_FinanceWithOtherRoles_PaysAnotherUsersExpense(string roles)
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, Roles(roles)), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
        Assert.IsNotNull(expense.Payment);
    }

    /// <summary>If another payment of the same expense was saved first (a concurrent payment), the answer is a conflict and nothing is saved.</summary>
    [TestMethod]
    public async Task PayAsync_ChangeRejectedWhileSaving_ReturnsWrongState()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);
        repository.ConflictOnNextSave = true;

        ExpenseOperationResult result = await NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id);

        Assert.AreEqual(ExpenseOperationStatus.WrongState, result.Status);
        Assert.AreEqual(1, repository.SaveAttempts);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>A persistence failure that is not a conflict is not hidden: it reaches the caller, after one single attempt to save, so the state, the payment and the history are never saved apart.</summary>
    [TestMethod]
    public async Task PayAsync_PersistenceFailure_Propagates_AfterASingleAttemptToSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);
        repository.FailOnNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(repository).PayAsync(ExpenseTestData.Caller(Payer, "Finance"), expense.Id));

        Assert.AreEqual(1, repository.SaveAttempts);
        Assert.AreEqual(0, repository.SaveCalls);
        Assert.AreEqual(1, repository.PaymentsAtLastSaveAttempt);
        Assert.AreEqual(2, repository.HistoryCountAtLastSaveAttempt);
    }

    /// <summary>A caller without an identifier cannot pay.</summary>
    /// <param name="userId">A user that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task PayAsync_CallerWithoutIdentifier_Throws(string? userId)
    {
        var (repository, expense) = Given(ExpenseStatus.Approved);

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewService(repository).PayAsync(ExpenseTestData.Caller(userId!, "Finance"), expense.Id));

        Assert.AreEqual(0, repository.SaveAttempts);
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
