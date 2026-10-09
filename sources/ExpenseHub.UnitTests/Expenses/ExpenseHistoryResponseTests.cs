using System;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the public representation of a history entry.
/// </summary>
[TestClass]
public sealed class ExpenseHistoryResponseTests
{
    /// <summary>Every field of the entry reaches the response, with the action and the states written as text.</summary>
    [TestMethod]
    public void From_RejectionEntry_CopiesEveryFieldAndWritesTheEnumsAsText()
    {
        var expenseId = Guid.NewGuid();
        var instant = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        var entry = new ExpenseHistory
        {
            Id = 7,
            ExpenseId = expenseId,
            Action = ExpenseHistoryAction.Rejected,
            ActorId = "approver-1",
            OccurredAtUtc = instant,
            PreviousStatus = ExpenseStatus.Submitted,
            NewStatus = ExpenseStatus.Rejected,
            Reason = "Nota fiscal ilegivel e sem CNPJ",
        };

        ExpenseHistoryResponse response = ExpenseHistoryResponse.From(entry);

        Assert.AreEqual(7, response.Id);
        Assert.AreEqual(expenseId, response.ExpenseId);
        Assert.AreEqual("Rejected", response.Action);
        Assert.AreEqual("approver-1", response.ActorId);
        Assert.AreEqual(instant, response.OccurredAtUtc);
        Assert.AreEqual("Submitted", response.PreviousStatus);
        Assert.AreEqual("Rejected", response.NewStatus);
        Assert.AreEqual("Nota fiscal ilegivel e sem CNPJ", response.Reason);
        Assert.IsNull(response.Changes);
    }

    /// <summary>The creation entry has no previous state, and the optional fields stay empty.</summary>
    [TestMethod]
    public void From_CreationEntry_HasNoPreviousStateReasonOrChanges()
    {
        var entry = new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Created,
            ActorId = "owner-1",
            NewStatus = ExpenseStatus.Draft,
        };

        ExpenseHistoryResponse response = ExpenseHistoryResponse.From(entry);

        Assert.AreEqual("Created", response.Action);
        Assert.IsNull(response.PreviousStatus);
        Assert.AreEqual("Draft", response.NewStatus);
        Assert.IsNull(response.Reason);
        Assert.IsNull(response.Changes);
    }

    /// <summary>The summary of an edition of a draft reaches the response.</summary>
    [TestMethod]
    public void From_EditionEntry_CopiesTheChanges()
    {
        var entry = new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Edited,
            ActorId = "owner-1",
            PreviousStatus = ExpenseStatus.Draft,
            NewStatus = ExpenseStatus.Draft,
            Changes = "Amount: 60.00 -> 90.00",
        };

        Assert.AreEqual("Amount: 60.00 -> 90.00", ExpenseHistoryResponse.From(entry).Changes);
    }

    /// <summary>The response has only internal identifiers and history data: no e-mail, user name or any other personal data of the actor.</summary>
    [TestMethod]
    public void ExpenseHistoryResponse_HasOnlyInternalIdentifiersAndHistoryData()
    {
        string[] members = typeof(ExpenseHistoryResponse).GetProperties()
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "Action",
            "ActorId",
            "Changes",
            "ExpenseId",
            "Id",
            "NewStatus",
            "OccurredAtUtc",
            "PreviousStatus",
            "Reason",
        ];

        CollectionAssert.AreEqual(expected, members);
    }
}
