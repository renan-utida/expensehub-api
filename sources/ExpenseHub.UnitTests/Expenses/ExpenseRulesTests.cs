using System;
using System.Linq;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the validation contract of an expense: description, amount and date.
/// </summary>
[TestClass]
public sealed class ExpenseRulesTests
{
    /// <summary>A description with 10 to 500 characters is valid, and spaces at the ends do not count.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(499)]
    [DataRow(500)]
    public void ValidateDescription_LengthInsideLimits_IsValid(int length)
    {
        Assert.IsNull(ExpenseRules.ValidateDescription(new string('a', length)));
    }

    /// <summary>Spaces around the description are ignored: the trimmed text is what must fit the limits.</summary>
    [TestMethod]
    public void ValidateDescription_PaddedWithSpaces_IsMeasuredAfterTrimming()
    {
        string padded = "   " + new string('a', 10) + "   ";

        Assert.IsNull(ExpenseRules.ValidateDescription(padded));
    }

    /// <summary>A description shorter than 10 or longer than 500 characters is rejected.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(1)]
    [DataRow(9)]
    [DataRow(501)]
    [DataRow(1000)]
    public void ValidateDescription_LengthOutsideLimits_IsInvalid(int length)
    {
        Assert.IsNotNull(ExpenseRules.ValidateDescription(new string('a', length)));
    }

    /// <summary>A missing, empty or blank description is rejected.</summary>
    /// <param name="description">A description that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("     ")]
    public void ValidateDescription_MissingOrBlank_IsInvalid(string? description)
    {
        Assert.IsNotNull(ExpenseRules.ValidateDescription(description));
    }

    /// <summary>Nine meaningful characters padded with spaces up to ten are still too short.</summary>
    [TestMethod]
    public void ValidateDescription_NineCharactersPaddedToTen_IsInvalid()
    {
        string padded = new string('a', 9) + " ";

        Assert.IsNotNull(ExpenseRules.ValidateDescription(padded));
    }

    /// <summary>An amount from 0.01 to Int32.MaxValue with at most two decimal places is valid.</summary>
    /// <param name="amount">The amount, written with a dot.</param>
    [TestMethod]
    [DataRow("0.01")]
    [DataRow("0.10")]
    [DataRow("1")]
    [DataRow("87.50")]
    [DataRow("1234.56")]
    [DataRow("2147483647")]
    [DataRow("2147483647.00")]
    public void ValidateAmount_InsideRangeWithTwoDecimals_IsValid(string amount)
    {
        Assert.IsNull(ExpenseRules.ValidateAmount(ExpenseTestData.Dec(amount)));
    }

    /// <summary>An amount below 0.01 or above Int32.MaxValue is rejected.</summary>
    /// <param name="amount">The amount, written with a dot.</param>
    [TestMethod]
    [DataRow("0")]
    [DataRow("0.00")]
    [DataRow("0.009")]
    [DataRow("0.001")]
    [DataRow("-0.01")]
    [DataRow("-100")]
    [DataRow("2147483647.01")]
    [DataRow("2147483648")]
    [DataRow("79228162514264337593543950335")]
    public void ValidateAmount_OutsideRange_IsInvalid(string amount)
    {
        Assert.IsNotNull(ExpenseRules.ValidateAmount(ExpenseTestData.Dec(amount)));
    }

    /// <summary>An amount with more than two decimal places is rejected, never rounded to fit.</summary>
    /// <param name="amount">The amount, written with a dot.</param>
    [TestMethod]
    [DataRow("12.345")]
    [DataRow("1.001")]
    [DataRow("10.005")]
    [DataRow("0.015")]
    public void ValidateAmount_MoreThanTwoDecimals_IsInvalid(string amount)
    {
        string? error = ExpenseRules.ValidateAmount(ExpenseTestData.Dec(amount));

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "decimal places");
    }

    /// <summary>A missing amount is rejected.</summary>
    [TestMethod]
    public void ValidateAmount_Missing_IsInvalid()
    {
        Assert.IsNotNull(ExpenseRules.ValidateAmount(null));
    }

    /// <summary>Today and past dates are valid.</summary>
    /// <param name="daysBeforeToday">How many days before today.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(365)]
    public void ValidateExpenseDate_TodayOrPast_IsValid(int daysBeforeToday)
    {
        DateOnly date = ExpenseTestData.Today.AddDays(-daysBeforeToday);

        Assert.IsNull(ExpenseRules.ValidateExpenseDate(date, ExpenseTestData.Today));
    }

    /// <summary>A future date is rejected.</summary>
    /// <param name="daysAfterToday">How many days after today.</param>
    [TestMethod]
    [DataRow(1)]
    [DataRow(30)]
    public void ValidateExpenseDate_Future_IsInvalid(int daysAfterToday)
    {
        DateOnly date = ExpenseTestData.Today.AddDays(daysAfterToday);

        Assert.IsNotNull(ExpenseRules.ValidateExpenseDate(date, ExpenseTestData.Today));
    }

    /// <summary>A missing date is rejected.</summary>
    [TestMethod]
    public void ValidateExpenseDate_Missing_IsInvalid()
    {
        Assert.IsNotNull(ExpenseRules.ValidateExpenseDate(null, ExpenseTestData.Today));
    }

    /// <summary>Valid data has no problems.</summary>
    [TestMethod]
    public void Validate_ValidData_ReturnsNoErrors()
    {
        ExpenseDetails data = ExpenseTestData.Valid;

        var errors = ExpenseRules.Validate(data.Description, data.Amount, data.ExpenseDate, ExpenseTestData.Today);

        Assert.IsEmpty(errors);
    }

    /// <summary>Every invalid field is reported, each one under its own name.</summary>
    [TestMethod]
    public void Validate_EverythingInvalid_ReportsEachFieldOnce()
    {
        var errors = ExpenseRules.Validate("curta", 0m, ExpenseTestData.Today.AddDays(1), ExpenseTestData.Today);

        string[] fields = errors.Select(error => error.Field).OrderBy(field => field).ToArray();

        Assert.HasCount(3, fields);
        Assert.AreEqual(nameof(ExpenseDetails.Amount), fields[0]);
        Assert.AreEqual(nameof(ExpenseDetails.Description), fields[1]);
        Assert.AreEqual(nameof(ExpenseDetails.ExpenseDate), fields[2]);
    }
}
