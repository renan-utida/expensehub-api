using System;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the public representation of a payment.
/// </summary>
[TestClass]
public sealed class PaymentResponseTests
{
    /// <summary>The response carries the expense, its new state and the actor and the instant recorded by the server.</summary>
    [TestMethod]
    public void From_PaidExpense_CarriesTheExpenseTheStateTheActorAndTheInstant()
    {
        var instant = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        Expense expense = ExpenseTestData.ExpenseOf("owner-1", ExpenseStatus.Paid);
        expense.Payment = new PaymentRecord { ActorId = "finance-1", PaidAtUtc = instant };

        PaymentResponse response = PaymentResponse.From(expense);

        Assert.AreEqual(expense.Id, response.ExpenseId);
        Assert.AreEqual("Paid", response.Status);
        Assert.AreEqual("finance-1", response.ActorId);
        Assert.AreEqual(instant, response.PaidAtUtc);
    }

    /// <summary>An expense without a payment record has no payment to show.</summary>
    [TestMethod]
    public void From_ExpenseWithoutPaymentRecord_Throws()
    {
        Expense expense = ExpenseTestData.ExpenseOf("owner-1", ExpenseStatus.Approved);

        Assert.ThrowsExactly<InvalidOperationException>(() => PaymentResponse.From(expense));
    }

    /// <summary>The response has only the expense, the state, the actor and the instant: nothing the client could assign and no personal data.</summary>
    [TestMethod]
    public void PaymentResponse_HasOnlyTheExpenseTheStateTheActorAndTheInstant()
    {
        string[] members = typeof(PaymentResponse).GetProperties()
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["ActorId", "ExpenseId", "PaidAtUtc", "Status"];

        CollectionAssert.AreEqual(expected, members);
    }
}
