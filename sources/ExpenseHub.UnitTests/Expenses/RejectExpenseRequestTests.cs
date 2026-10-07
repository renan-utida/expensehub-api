using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the rejection request: its data annotations and the fact that it has no field the client must not set.
/// </summary>
[TestClass]
public sealed class RejectExpenseRequestTests
{
    /// <summary>The request has only the reason: no state, actor or instant to assign.</summary>
    [TestMethod]
    public void RejectExpenseRequest_HasOnlyTheReason()
    {
        string[] members = typeof(RejectExpenseRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.HasCount(1, members);
        Assert.AreEqual(nameof(RejectExpenseRequest.Reason), members[0]);
    }

    /// <summary>A valid reason passes the annotations.</summary>
    [TestMethod]
    public void Validate_ValidReason_HasNoErrors()
    {
        Assert.IsEmpty(Validate(NewRequest("Nota fiscal ilegivel e sem CNPJ")));
    }

    /// <summary>A missing, empty or blank reason is rejected.</summary>
    /// <param name="reason">A reason that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Validate_MissingReason_ReportsReasonError(string? reason)
    {
        CollectionAssert.Contains(Validate(NewRequest(reason)), nameof(RejectExpenseRequest.Reason));
    }

    /// <summary>A reason with fewer than 10 or more than 500 characters is rejected.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(9)]
    [DataRow(501)]
    public void Validate_ReasonOutsideLimits_ReportsReasonError(int length)
    {
        CollectionAssert.Contains(Validate(NewRequest(new string('a', length))), nameof(RejectExpenseRequest.Reason));
    }

    /// <summary>A reason with 10 or 500 characters is accepted.</summary>
    /// <param name="length">The number of characters.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Validate_ReasonOnTheLimits_HasNoErrors(int length)
    {
        Assert.IsEmpty(Validate(NewRequest(new string('a', length))));
    }

    /// <summary>Spaces around the reason are trimmed before the length is measured, so ten characters with spaces around them are accepted.</summary>
    [TestMethod]
    public void Validate_TenCharactersPaddedWithSpaces_HasNoErrors()
    {
        Assert.IsEmpty(Validate(NewRequest("   " + new string('a', 10) + "   ")));
    }

    /// <summary>Spaces do not make a short reason valid: nine characters with spaces around them are still too short.</summary>
    [TestMethod]
    public void Validate_NineCharactersPaddedWithSpaces_ReportsReasonError()
    {
        CollectionAssert.Contains(Validate(NewRequest("   " + new string('a', 9) + "   ")), nameof(RejectExpenseRequest.Reason));
    }

    /// <summary>Spaces around a reason of exactly 500 characters do not make it too long.</summary>
    [TestMethod]
    public void Validate_FiveHundredCharactersPaddedWithSpaces_HasNoErrors()
    {
        Assert.IsEmpty(Validate(NewRequest("  " + new string('a', 500) + "  ")));
    }

    private static RejectExpenseRequest NewRequest(string? reason)
    {
        return new RejectExpenseRequest { Reason = reason };
    }

    private static List<string> Validate(RejectExpenseRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).ToList();
    }
}
