using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the validation contract of a report request: the scope is exactly Mine or Global, both dates are present and the
/// period does not end before it starts.
/// </summary>
[TestClass]
public sealed class ReportRulesTests
{
    /// <summary>The names Mine and Global are the two valid scopes.</summary>
    /// <param name="text">The name.</param>
    /// <param name="expected">The scope it stands for.</param>
    [TestMethod]
    [DataRow("Mine", ReportScope.Mine)]
    [DataRow("Global", ReportScope.Global)]
    public void TryParseScope_ExactName_ReturnsTheScope(string text, ReportScope expected)
    {
        Assert.IsTrue(ReportRules.TryParseScope(text, out ReportScope scope));
        Assert.AreEqual(expected, scope);
    }

    /// <summary>Anything else is not a scope: numbers (also in quotes), other letter case, blanks, padding and unknown names.</summary>
    /// <param name="text">The text.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("2")]
    [DataRow("mine")]
    [DataRow("GLOBAL")]
    [DataRow(" Mine")]
    [DataRow("Mine ")]
    [DataRow("Everyone")]
    [DataRow("Mine,Global")]
    public void TryParseScope_AnythingElse_IsNotAScope(string? text)
    {
        Assert.IsFalse(ReportRules.TryParseScope(text, out _));
        Assert.IsNotNull(ReportRules.ValidateScope(text));
    }

    /// <summary>A valid scope has no error.</summary>
    /// <param name="text">The name.</param>
    [TestMethod]
    [DataRow("Mine")]
    [DataRow("Global")]
    public void ValidateScope_ValidName_HasNoError(string text)
    {
        Assert.IsNull(ReportRules.ValidateScope(text));
    }

    /// <summary>A period whose start is before its end is valid, and so is a period of a single day.</summary>
    [TestMethod]
    public void ValidatePeriodOrder_StartBeforeOrEqualToEnd_IsValid()
    {
        Assert.IsNull(ReportRules.ValidatePeriodOrder(ReportTestData.From, ReportTestData.To));
        Assert.IsNull(ReportRules.ValidatePeriodOrder(ReportTestData.From, ReportTestData.From));
    }

    /// <summary>A period whose start is after its end is invalid.</summary>
    [TestMethod]
    public void ValidatePeriodOrder_StartAfterEnd_IsInvalid()
    {
        Assert.IsNotNull(ReportRules.ValidatePeriodOrder(ReportTestData.To, ReportTestData.From));
    }

    /// <summary>A missing date is not an order problem: it is reported as a required field instead.</summary>
    [TestMethod]
    public void ValidatePeriodOrder_MissingDate_IsLeftToTheRequiredCheck()
    {
        Assert.IsNull(ReportRules.ValidatePeriodOrder(null, ReportTestData.To));
        Assert.IsNull(ReportRules.ValidatePeriodOrder(ReportTestData.From, null));
        Assert.IsNull(ReportRules.ValidatePeriodOrder(null, null));
    }

    /// <summary>Valid data has no error.</summary>
    [TestMethod]
    public void Validate_ValidData_HasNoErrors()
    {
        Assert.IsEmpty(ReportRules.Validate(ReportTestData.Details()));
    }

    /// <summary>A missing start date, end date or scope is an error on its own field.</summary>
    [TestMethod]
    public void Validate_MissingFields_ReportEachField()
    {
        IReadOnlyList<ReportValidationError> errors = ReportRules.Validate(new ReportRequestDetails(null, null, null));

        CollectionAssert.AreEquivalent(
            new[] { ReportRules.FromDateField, ReportRules.ToDateField, ReportRules.ScopeField },
            errors.Select(error => error.Field).ToArray());
    }

    /// <summary>A start date after the end date is an error on the start date.</summary>
    [TestMethod]
    public void Validate_StartAfterEnd_ReportsTheStartDate()
    {
        var details = new ReportRequestDetails(ReportTestData.To, ReportTestData.From, "Mine");

        ReportValidationError error = ReportRules.Validate(details).Single();

        Assert.AreEqual(ReportRules.FromDateField, error.Field);
    }

    /// <summary>An unknown scope is an error on the scope, and the dates are still checked.</summary>
    [TestMethod]
    public void Validate_UnknownScope_ReportsTheScope()
    {
        ReportValidationError error = ReportRules.Validate(ReportTestData.Details("Team")).Single();

        Assert.AreEqual(ReportRules.ScopeField, error.Field);
    }

    /// <summary>The rules do not accept missing details.</summary>
    [TestMethod]
    public void Validate_MissingDetails_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportRules.Validate(null!));
    }

    /// <summary>The names of the enums are the ones of the contract: the scopes and the four states of a report.</summary>
    [TestMethod]
    public void Enums_HaveTheNamesOfTheContract()
    {
        string[] scopes = ["Mine", "Global"];
        string[] states = ["Pending", "Processing", "Completed", "Failed"];

        CollectionAssert.AreEqual(scopes, Enum.GetNames<ReportScope>());
        CollectionAssert.AreEqual(states, Enum.GetNames<ReportStatus>());
    }
}
