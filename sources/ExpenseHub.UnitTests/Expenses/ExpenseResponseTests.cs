using System;
using System.Linq;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the public representation of an expense.
/// </summary>
[TestClass]
public sealed class ExpenseResponseTests
{
    /// <summary>The response of a list or detail has only the fields of the expense: no history, no payment and no e-mail or other personal data of the owner, who appears only by the internal identifier.</summary>
    [TestMethod]
    public void ExpenseResponse_HasNoHistoryPaymentOrPersonalData()
    {
        string[] members = typeof(ExpenseResponse).GetProperties()
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "Amount",
            "CreatedAtUtc",
            "Description",
            "ExpenseDate",
            "Id",
            "OwnerId",
            "Status",
        ];

        CollectionAssert.AreEqual(expected, members);
    }
}
