using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the rule that decides whether a bearer token is still valid after a change of roles.
/// </summary>
[TestClass]
public sealed class SecurityStampCheckTests
{
    /// <summary>A token with the same stamp that is stored for the user is still valid.</summary>
    [TestMethod]
    public void IsCurrent_SameStamp_ReturnsTrue()
    {
        Assert.IsTrue(SecurityStampCheck.IsCurrent("stamp-a", "stamp-a"));
    }

    /// <summary>After the roles change the stored stamp changes, so the old token is no longer valid.</summary>
    [TestMethod]
    public void IsCurrent_StampRenewedAfterRoleChange_ReturnsFalse()
    {
        Assert.IsFalse(SecurityStampCheck.IsCurrent("stamp-a", "stamp-b"));
    }

    /// <summary>The comparison is exact, so a stamp that differs only in letter case is not valid.</summary>
    [TestMethod]
    public void IsCurrent_StampInOtherCase_ReturnsFalse()
    {
        Assert.IsFalse(SecurityStampCheck.IsCurrent("STAMP-A", "stamp-a"));
    }

    /// <summary>A token without the stamp claim is not valid.</summary>
    /// <param name="stored">The stamp stored for the user.</param>
    [TestMethod]
    [DataRow("stamp-a")]
    [DataRow("")]
    [DataRow(null)]
    public void IsCurrent_TokenWithoutStamp_ReturnsFalse(string? stored)
    {
        Assert.IsFalse(SecurityStampCheck.IsCurrent(null, stored));
    }

    /// <summary>A user that no longer exists (no stored stamp) makes its tokens invalid.</summary>
    /// <param name="issued">The stamp carried by the token.</param>
    [TestMethod]
    [DataRow("stamp-a")]
    [DataRow("")]
    public void IsCurrent_UserWithoutStoredStamp_ReturnsFalse(string issued)
    {
        Assert.IsFalse(SecurityStampCheck.IsCurrent(issued, null));
    }
}
