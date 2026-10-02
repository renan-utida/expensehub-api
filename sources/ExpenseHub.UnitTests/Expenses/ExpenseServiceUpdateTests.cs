using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the edition of drafts, using a fake repository and a fixed clock, with no database.
/// </summary>
[TestClass]
public sealed class ExpenseServiceUpdateTests
{
    private const string Owner = "owner-1";
    private const string Other = "owner-2";

    /// <summary>The owner replaces the three fields of a draft.</summary>
    [TestMethod]
    public async Task UpdateAsync_OwnerEditsDraft_ReplacesTheThreeFields()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Owner, expense.Id, ExpenseTestData.Valid);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.AreEqual(ExpenseTestData.Valid.Description, expense.Description);
        Assert.AreEqual(87.50m, expense.Amount);
        Assert.AreEqual(new DateOnly(2026, 10, 1), expense.ExpenseDate);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    /// <summary>The edition never changes the owner, the state or the creation instant.</summary>
    [TestMethod]
    public async Task UpdateAsync_OwnerEditsDraft_KeepsOwnerStateAndCreationInstant()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        Guid id = expense.Id;
        DateTimeOffset createdAt = expense.CreatedAtUtc;

        await NewService(repository).UpdateAsync(Owner, id, ExpenseTestData.Valid);

        Assert.AreEqual(id, expense.Id);
        Assert.AreEqual(Owner, expense.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(createdAt, expense.CreatedAtUtc);
    }

    /// <summary>The edition is recorded in the history in the same save as the change.</summary>
    [TestMethod]
    public async Task UpdateAsync_OwnerEditsDraft_RecordsTheEditionInTheSameSave()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).UpdateAsync(Owner, expense.Id, ExpenseTestData.Valid);

        Assert.AreEqual(2, repository.HistoryCountAtLastSave);
        ExpenseHistory entry = expense.History.Last();
        Assert.AreEqual(ExpenseHistoryAction.Edited, entry.Action);
        Assert.AreEqual(Owner, entry.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Draft, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, entry.NewStatus);
    }

    /// <summary>The history says what changed, with the old and the new value.</summary>
    [TestMethod]
    public async Task UpdateAsync_ChangedFields_AreDescribedInTheHistory()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await NewService(repository).UpdateAsync(Owner, expense.Id, ExpenseTestData.Valid);

        string? changes = expense.History.Last().Changes;
        Assert.IsNotNull(changes);
        StringAssert.Contains(changes, "Description: \"Taxi para o aeroporto\" -> \"Almoco com cliente em Campinas\"");
        StringAssert.Contains(changes, "Amount: 60.00 -> 87.50");
        StringAssert.Contains(changes, "ExpenseDate: 2026-09-20 -> 2026-10-01");
    }

    /// <summary>Only the fields that really changed are listed in the history.</summary>
    [TestMethod]
    public async Task UpdateAsync_OnlyTheAmountChanges_ListsOnlyTheAmount()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        var details = new ExpenseDetails(expense.Description, 75.25m, expense.ExpenseDate);

        await NewService(repository).UpdateAsync(Owner, expense.Id, details);

        Assert.AreEqual("Amount: 60.00 -> 75.25", expense.History.Last().Changes);
    }

    /// <summary>Sending the values already stored still records the edition, saying no field changed.</summary>
    [TestMethod]
    public async Task UpdateAsync_SameValues_RecordsAnEditionWithNoFieldChanged()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        var details = new ExpenseDetails(expense.Description, expense.Amount, expense.ExpenseDate);

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Owner, expense.Id, details);

        Assert.AreEqual(ExpenseOperationStatus.Succeeded, result.Status);
        Assert.HasCount(2, expense.History);
        Assert.AreEqual("No field changed.", expense.History.Last().Changes);
    }

    /// <summary>The summary of the changes always fits the limit of the column.</summary>
    [TestMethod]
    public async Task UpdateAsync_VeryLongDescriptions_KeepTheSummaryWithinTheLimit()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        expense.Description = new string('x', 500);
        var details = new ExpenseDetails(new string('y', 500), 1m, ExpenseTestData.Today);

        await NewService(repository).UpdateAsync(Owner, expense.Id, details);

        Assert.IsLessThanOrEqualTo(ExpenseRules.ChangesMaxLength, expense.History.Last().Changes!.Length);
    }

    /// <summary>Spaces around the description are not stored.</summary>
    [TestMethod]
    public async Task UpdateAsync_DescriptionWithSpacesAtTheEnds_StoresItTrimmed()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        var details = new ExpenseDetails("   Almoco com cliente em Campinas   ", 10m, ExpenseTestData.Today);

        await NewService(repository).UpdateAsync(Owner, expense.Id, details);

        Assert.AreEqual("Almoco com cliente em Campinas", expense.Description);
    }

    /// <summary>Another user cannot edit the draft: it looks as if it did not exist, and nothing changes.</summary>
    [TestMethod]
    public async Task UpdateAsync_DraftOfAnotherUser_ReturnsNotFoundAndChangesNothing()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Other, expense.Id, ExpenseTestData.Valid);

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An expense that does not exist is not found.</summary>
    [TestMethod]
    public async Task UpdateAsync_MissingExpense_ReturnsNotFound()
    {
        var repository = new FakeExpenseRepository();

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Owner, Guid.NewGuid(), ExpenseTestData.Valid);

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>An expense that is not a draft is not edited, no history is written and nothing is saved.</summary>
    /// <param name="status">A state other than Draft.</param>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task UpdateAsync_ExpenseOutsideDraft_ReturnsNotDraftAndChangesNothing(ExpenseStatus status)
    {
        var (repository, expense) = Given(status);

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Owner, expense.Id, ExpenseTestData.Valid);

        Assert.AreEqual(ExpenseOperationStatus.NotDraft, result.Status);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.AreEqual(status, expense.Status);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Invalid data is rejected, and the draft and its history stay as they were.</summary>
    [TestMethod]
    public async Task UpdateAsync_InvalidData_IsRejectedAndChangesNothing()
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);
        var details = new ExpenseDetails("curta", 12.345m, ExpenseTestData.Today.AddDays(1));

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Owner, expense.Id, details);

        Assert.AreEqual(ExpenseOperationStatus.ValidationFailed, result.Status);
        Assert.HasCount(3, result.Errors);
        Assert.AreEqual("Taxi para o aeroporto", expense.Description);
        Assert.AreEqual(60.00m, expense.Amount);
        Assert.HasCount(1, expense.History);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    /// <summary>Invalid data is reported first, even when the expense does not exist or is not a draft.</summary>
    [TestMethod]
    public async Task UpdateAsync_InvalidDataForAnExpenseThatCannotBeEdited_ReportsTheValidationFirst()
    {
        var (repository, submitted) = Given(ExpenseStatus.Submitted);
        var invalid = new ExpenseDetails("curta", 10m, ExpenseTestData.Today);
        ExpenseService service = NewService(repository);

        ExpenseOperationResult notDraft = await service.UpdateAsync(Owner, submitted.Id, invalid);
        ExpenseOperationResult missing = await service.UpdateAsync(Owner, Guid.NewGuid(), invalid);

        Assert.AreEqual(ExpenseOperationStatus.ValidationFailed, notDraft.Status);
        Assert.AreEqual(ExpenseOperationStatus.ValidationFailed, missing.Status);
    }

    /// <summary>Someone else's expense is not found even when it is also outside Draft.</summary>
    [TestMethod]
    public async Task UpdateAsync_SubmittedExpenseOfAnotherUser_ReturnsNotFoundBeforeNotDraft()
    {
        var (repository, expense) = Given(ExpenseStatus.Submitted);

        ExpenseOperationResult result = await NewService(repository).UpdateAsync(Other, expense.Id, ExpenseTestData.Valid);

        Assert.AreEqual(ExpenseOperationStatus.NotFound, result.Status);
    }

    /// <summary>A caller without an identifier cannot edit a draft.</summary>
    /// <param name="actorId">An actor that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public async Task UpdateAsync_CallerWithoutIdentifier_Throws(string? actorId)
    {
        var (repository, expense) = Given(ExpenseStatus.Draft);

        await Assert.ThrowsAsync<ArgumentException>(() => NewService(repository).UpdateAsync(actorId!, expense.Id, ExpenseTestData.Valid));

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
