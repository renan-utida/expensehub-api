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
/// Tests of the reading of expenses by profile, using a fake repository that applies the scope, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServiceQueryTests
{
    private const string Me = "me";
    private const string Someone = "someone";

    /// <summary>An Employee lists only its own expenses.</summary>
    [TestMethod]
    public async Task ListAsync_Employee_ReturnsOnlyItsOwnExpenses()
    {
        FakeExpenseRepository repository = SeededRepository();

        var visible = await NewService(repository).ListAsync(ExpenseTestData.Caller(Me, "Employee"));

        Assert.AreEqual("myDraft,mySubmitted", Labels(visible));
    }

    /// <summary>An Approver lists only the submitted expenses, from any owner.</summary>
    [TestMethod]
    public async Task ListAsync_Approver_ReturnsOnlySubmittedExpenses()
    {
        FakeExpenseRepository repository = SeededRepository();

        var visible = await NewService(repository).ListAsync(ExpenseTestData.Caller("approver-1", "Approver"));

        Assert.AreEqual("mySubmitted,otherSubmitted", Labels(visible));
    }

    /// <summary>A Finance user lists only the approved and the paid expenses.</summary>
    [TestMethod]
    public async Task ListAsync_Finance_ReturnsOnlyApprovedAndPaidExpenses()
    {
        FakeExpenseRepository repository = SeededRepository();

        var visible = await NewService(repository).ListAsync(ExpenseTestData.Caller("finance-1", "Finance"));

        Assert.AreEqual("otherApproved,otherPaid", Labels(visible));
    }

    /// <summary>An Auditor lists every expense.</summary>
    [TestMethod]
    public async Task ListAsync_Auditor_ReturnsEveryExpense()
    {
        FakeExpenseRepository repository = SeededRepository();

        var visible = await NewService(repository).ListAsync(ExpenseTestData.Caller("auditor-1", "Auditor"));

        Assert.HasCount(repository.Expenses.Count, visible);
    }

    /// <summary>Roles accumulate: an Employee who is also an Approver lists its own expenses plus the submitted ones.</summary>
    [TestMethod]
    public async Task ListAsync_EmployeeAndApprover_ReturnsTheUnion()
    {
        FakeExpenseRepository repository = SeededRepository();

        var visible = await NewService(repository).ListAsync(ExpenseTestData.Caller(Me, "Employee", "Approver"));

        Assert.AreEqual("myDraft,mySubmitted,otherSubmitted", Labels(visible));
    }

    /// <summary>The Admin role alone lists nothing, and the repository is not even asked.</summary>
    [TestMethod]
    public async Task ListAsync_AdminAlone_ReturnsNothingWithoutAskingTheRepository()
    {
        FakeExpenseRepository repository = SeededRepository();

        var visible = await NewService(repository).ListAsync(ExpenseTestData.Caller(Me, "Admin"));

        Assert.IsEmpty(visible);
        Assert.AreEqual(0, repository.ListCalls);
    }

    /// <summary>An Employee gets its own expense.</summary>
    [TestMethod]
    public async Task GetAsync_OwnExpense_ReturnsIt()
    {
        FakeExpenseRepository repository = SeededRepository();
        Guid id = IdOf(repository, "myDraft");

        Expense? expense = await NewService(repository).GetAsync(ExpenseTestData.Caller(Me, "Employee"), id);

        Assert.IsNotNull(expense);
        Assert.AreEqual("myDraft", expense.Description);
    }

    /// <summary>An expense of someone else is not returned to an Employee, whatever its state.</summary>
    /// <param name="label">The label of an expense of someone else.</param>
    [TestMethod]
    [DataRow("otherDraft")]
    [DataRow("otherSubmitted")]
    [DataRow("otherApproved")]
    [DataRow("otherPaid")]
    public async Task GetAsync_ExpenseOfSomeoneElseAsEmployee_ReturnsNull(string label)
    {
        FakeExpenseRepository repository = SeededRepository();

        Expense? expense = await NewService(repository).GetAsync(ExpenseTestData.Caller(Me, "Employee"), IdOf(repository, label));

        Assert.IsNull(expense);
    }

    /// <summary>An Approver gets a submitted expense of someone else, but not its draft.</summary>
    [TestMethod]
    public async Task GetAsync_Approver_ReadsSubmittedButNotDraft()
    {
        FakeExpenseRepository repository = SeededRepository();
        ExpenseService service = NewService(repository);
        ExpenseCaller approver = ExpenseTestData.Caller("approver-1", "Approver");

        Expense? submitted = await service.GetAsync(approver, IdOf(repository, "otherSubmitted"));
        Expense? draft = await service.GetAsync(approver, IdOf(repository, "otherDraft"));

        Assert.IsNotNull(submitted);
        Assert.IsNull(draft);
    }

    /// <summary>A Finance user gets an approved or paid expense, but not a submitted one.</summary>
    [TestMethod]
    public async Task GetAsync_Finance_ReadsApprovedAndPaidButNotSubmitted()
    {
        FakeExpenseRepository repository = SeededRepository();
        ExpenseService service = NewService(repository);
        ExpenseCaller finance = ExpenseTestData.Caller("finance-1", "Finance");

        Assert.IsNotNull(await service.GetAsync(finance, IdOf(repository, "otherApproved")));
        Assert.IsNotNull(await service.GetAsync(finance, IdOf(repository, "otherPaid")));
        Assert.IsNull(await service.GetAsync(finance, IdOf(repository, "otherSubmitted")));
    }

    /// <summary>An Auditor gets an expense in any state.</summary>
    [TestMethod]
    public async Task GetAsync_Auditor_ReadsAnyState()
    {
        FakeExpenseRepository repository = SeededRepository();
        ExpenseService service = NewService(repository);
        ExpenseCaller auditor = ExpenseTestData.Caller("auditor-1", "Auditor");

        foreach (Expense expense in repository.Expenses)
        {
            Assert.IsNotNull(await service.GetAsync(auditor, expense.Id), expense.Description);
        }
    }

    /// <summary>An expense that does not exist and one outside the scope are indistinguishable: both are null.</summary>
    [TestMethod]
    public async Task GetAsync_MissingAndInvisibleExpenses_AreIndistinguishable()
    {
        FakeExpenseRepository repository = SeededRepository();
        ExpenseService service = NewService(repository);
        ExpenseCaller employee = ExpenseTestData.Caller(Me, "Employee");

        Expense? missing = await service.GetAsync(employee, Guid.NewGuid());
        Expense? invisible = await service.GetAsync(employee, IdOf(repository, "otherDraft"));

        Assert.IsNull(missing);
        Assert.IsNull(invisible);
    }

    /// <summary>The Admin role alone gets nothing, not even an expense it owns.</summary>
    [TestMethod]
    public async Task GetAsync_AdminAlone_ReturnsNull()
    {
        FakeExpenseRepository repository = SeededRepository();

        Expense? expense = await NewService(repository).GetAsync(ExpenseTestData.Caller(Me, "Admin"), IdOf(repository, "myDraft"));

        Assert.IsNull(expense);
    }

    private static FakeExpenseRepository SeededRepository()
    {
        var repository = new FakeExpenseRepository();
        repository.Given(ExpenseTestData.Labeled("myDraft", Me, ExpenseStatus.Draft));
        repository.Given(ExpenseTestData.Labeled("mySubmitted", Me, ExpenseStatus.Submitted));
        repository.Given(ExpenseTestData.Labeled("otherDraft", Someone, ExpenseStatus.Draft));
        repository.Given(ExpenseTestData.Labeled("otherSubmitted", Someone, ExpenseStatus.Submitted));
        repository.Given(ExpenseTestData.Labeled("otherApproved", Someone, ExpenseStatus.Approved));
        repository.Given(ExpenseTestData.Labeled("otherPaid", Someone, ExpenseStatus.Paid));

        return repository;
    }

    private static Guid IdOf(FakeExpenseRepository repository, string label)
    {
        return repository.Expenses.Single(expense => expense.Description == label).Id;
    }

    private static string Labels(IReadOnlyList<Expense> expenses)
    {
        return string.Join(',', expenses.Select(expense => expense.Description).OrderBy(label => label, StringComparer.Ordinal));
    }

    private static ExpenseService NewService(FakeExpenseRepository repository)
    {
        return new ExpenseService(repository, new FixedTimeProvider(ExpenseTestData.Now));
    }
}
