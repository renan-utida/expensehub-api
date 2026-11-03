using System;
using ExpenseHub.Api.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of which failures of SQLite are transient, so the worker waits and tries again instead of marking the report as failed.
/// They work on result codes and on exception objects built by hand; no database is opened.
/// </summary>
[TestClass]
public sealed class SqliteTransientErrorsTests
{
    /// <summary>A busy or locked database, memory, disk or file problem is transient, with the primary and the extended codes.</summary>
    /// <param name="code">The result code.</param>
    [TestMethod]
    [DataRow(5)]
    [DataRow(261)]
    [DataRow(517)]
    [DataRow(773)]
    [DataRow(6)]
    [DataRow(262)]
    [DataRow(7)]
    [DataRow(10)]
    [DataRow(266)]
    [DataRow(522)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(270)]
    public void IsTransient_Code_IsTrueForTheCodesThatPass(int code)
    {
        Assert.IsTrue(SqliteTransientErrors.IsTransient(code));
    }

    /// <summary>A constraint, an error of the statement, a read-only database or no error at all is not transient.</summary>
    /// <param name="code">The result code.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(11)]
    [DataRow(19)]
    [DataRow(275)]
    [DataRow(2067)]
    [DataRow(1299)]
    [DataRow(100)]
    public void IsTransient_Code_IsFalseForTheOthers(int code)
    {
        Assert.IsFalse(SqliteTransientErrors.IsTransient(code));
    }

    /// <summary>An exception of the provider is judged by its extended code, and by the primary one when there is none.</summary>
    [TestMethod]
    public void IsTransient_SqliteException_UsesTheResultCode()
    {
        Assert.IsTrue(SqliteTransientErrors.IsTransient(new SqliteException("busy", 5)));
        Assert.IsTrue(SqliteTransientErrors.IsTransient(new SqliteException("busy", 5, 517)));
        Assert.IsTrue(SqliteTransientErrors.IsTransient(new SqliteException("cannot open", 14)));
        Assert.IsFalse(SqliteTransientErrors.IsTransient(new SqliteException("constraint", 19)));
        Assert.IsFalse(SqliteTransientErrors.IsTransient(new SqliteException("unique", 19, 2067)));
    }

    /// <summary>The same failure wrapped by the update of Entity Framework Core is judged by what it wraps.</summary>
    [TestMethod]
    public void IsTransient_WrappedSqliteException_IsJudgedByTheInnerFailure()
    {
        var busy = new DbUpdateException("update", new SqliteException("busy", 5));
        var constraint = new DbUpdateException("update", new SqliteException("constraint", 19));

        Assert.IsTrue(SqliteTransientErrors.IsTransient(busy));
        Assert.IsFalse(SqliteTransientErrors.IsTransient(constraint));
    }

    /// <summary>Anything else is not transient: other exceptions, an update without an inner failure, and no exception.</summary>
    [TestMethod]
    public void IsTransient_OtherExceptions_AreNotTransient()
    {
        Assert.IsFalse(SqliteTransientErrors.IsTransient(new InvalidOperationException("not the database")));
        Assert.IsFalse(SqliteTransientErrors.IsTransient(new DbUpdateException("update")));
        Assert.IsFalse(SqliteTransientErrors.IsTransient(new DbUpdateConcurrencyException("race")));
        Assert.IsFalse(SqliteTransientErrors.IsTransient((Exception?)null));
    }
}
