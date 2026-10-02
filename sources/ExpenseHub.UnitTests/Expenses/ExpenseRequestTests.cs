using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the expense request: its data annotations and the fact that it has no field the client must not set.
/// </summary>
[TestClass]
public sealed class ExpenseRequestTests
{
    /// <summary>The request has only the three fields the client controls: no owner, state, actor or instant to assign.</summary>
    [TestMethod]
    public void ExpenseRequest_HasOnlyDescriptionAmountAndDate()
    {
        string[] members = typeof(ExpenseRequest).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.HasCount(3, members);
        Assert.AreEqual(nameof(ExpenseRequest.Amount), members[0]);
        Assert.AreEqual(nameof(ExpenseRequest.Description), members[1]);
        Assert.AreEqual(nameof(ExpenseRequest.ExpenseDate), members[2]);
    }

    /// <summary>Valid data passes the annotations.</summary>
    [TestMethod]
    public void Validate_ValidData_HasNoErrors()
    {
        Assert.IsEmpty(Validate(NewRequest("Almoco com cliente em Campinas", 87.50m, ExpenseTestData.Today)));
    }

    /// <summary>A missing description is rejected.</summary>
    /// <param name="description">A description that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Validate_MissingDescription_ReportsDescriptionError(string? description)
    {
        CollectionAssert.Contains(Validate(NewRequest(description, 10m, ExpenseTestData.Today)), nameof(ExpenseRequest.Description));
    }

    /// <summary>A description with fewer than 10 or more than 500 characters is rejected.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(9)]
    [DataRow(501)]
    public void Validate_DescriptionOutsideLimits_ReportsDescriptionError(int length)
    {
        CollectionAssert.Contains(Validate(NewRequest(new string('a', length), 10m, ExpenseTestData.Today)), nameof(ExpenseRequest.Description));
    }

    /// <summary>A description with 10 or 500 characters is accepted.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Validate_DescriptionOnTheLimits_HasNoErrors(int length)
    {
        Assert.IsEmpty(Validate(NewRequest(new string('a', length), 10m, ExpenseTestData.Today)));
    }

    /// <summary>A missing amount is rejected, so an omitted field is never read as zero.</summary>
    [TestMethod]
    public void Validate_MissingAmount_ReportsAmountError()
    {
        CollectionAssert.Contains(Validate(NewRequest("Almoco com cliente em Campinas", null, ExpenseTestData.Today)), nameof(ExpenseRequest.Amount));
    }

    /// <summary>An amount below 0.01, above Int32.MaxValue or with more than two decimals is rejected.</summary>
    /// <param name="amount">The amount, written with a dot.</param>
    [TestMethod]
    [DataRow("0")]
    [DataRow("0.009")]
    [DataRow("-1")]
    [DataRow("12.345")]
    [DataRow("2147483647.01")]
    public void Validate_InvalidAmount_ReportsAmountError(string amount)
    {
        CollectionAssert.Contains(
            Validate(NewRequest("Almoco com cliente em Campinas", ExpenseTestData.Dec(amount), ExpenseTestData.Today)),
            nameof(ExpenseRequest.Amount));
    }

    /// <summary>The smallest and the largest amounts are accepted.</summary>
    /// <param name="amount">The amount, written with a dot.</param>
    [TestMethod]
    [DataRow("0.01")]
    [DataRow("2147483647")]
    public void Validate_AmountOnTheLimits_HasNoErrors(string amount)
    {
        Assert.IsEmpty(Validate(NewRequest("Almoco com cliente em Campinas", ExpenseTestData.Dec(amount), ExpenseTestData.Today)));
    }

    /// <summary>A missing date is rejected.</summary>
    [TestMethod]
    public void Validate_MissingDate_ReportsDateError()
    {
        CollectionAssert.Contains(Validate(NewRequest("Almoco com cliente em Campinas", 10m, null)), nameof(ExpenseRequest.ExpenseDate));
    }

    private static ExpenseRequest NewRequest(string? description, decimal? amount, DateOnly? expenseDate)
    {
        return new ExpenseRequest { Description = description, Amount = amount, ExpenseDate = expenseDate };
    }

    private static List<string> Validate(ExpenseRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).ToList();
    }
}
