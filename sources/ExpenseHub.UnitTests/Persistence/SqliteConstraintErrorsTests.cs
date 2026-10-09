using ExpenseHub.Api.Persistence.Repositories;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Persistence;

/// <summary>
/// Tests of the classification of SQLite constraint failures, which uses plain result codes and needs no database.
/// </summary>
[TestClass]
public sealed class SqliteConstraintErrorsTests
{
    /// <summary>A constraint failure with the unique index extended code is a unique violation.</summary>
    [TestMethod]
    public void IsUniqueConstraint_UniqueIndexViolation_IsTrue()
    {
        Assert.IsTrue(SqliteConstraintErrors.IsUniqueConstraint(19, 2067));
    }

    /// <summary>Other failures are not unique violations: a foreign key, a check, a trigger, a busy database and a generic error.</summary>
    /// <param name="errorCode">The result code.</param>
    /// <param name="extendedErrorCode">The extended result code.</param>
    [TestMethod]
    [DataRow(19, 787)]
    [DataRow(19, 275)]
    [DataRow(19, 1811)]
    [DataRow(5, 5)]
    [DataRow(1, 0)]
    public void IsUniqueConstraint_OtherFailures_IsFalse(int errorCode, int extendedErrorCode)
    {
        Assert.IsFalse(SqliteConstraintErrors.IsUniqueConstraint(errorCode, extendedErrorCode));
    }
}
