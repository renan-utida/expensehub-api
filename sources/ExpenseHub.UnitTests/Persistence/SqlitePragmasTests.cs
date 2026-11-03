using System.Linq;
using ExpenseHub.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Persistence;

/// <summary>
/// Tests of the settings that let the web application and the worker share one SQLite file. The commands are plain text and the
/// connection string is only parsed, so no database is opened. What happens in the database when two processes open it together
/// was checked by hand against a real file.
/// </summary>
[TestClass]
public sealed class SqlitePragmasTests
{
    /// <summary>The busy timeout is set before the write-ahead log, because changing the mode needs a lock and the connection must already know how long to wait.</summary>
    [TestMethod]
    public void OnOpen_SetsTheBusyTimeoutBeforeTheWriteAheadLog()
    {
        string[] commands = SqlitePragmas.OnOpen.ToArray();

        Assert.HasCount(2, commands);
        Assert.AreEqual("PRAGMA busy_timeout = 5000;", commands[0]);
        Assert.AreEqual("PRAGMA journal_mode = WAL;", commands[1]);
    }

    /// <summary>The interceptor and the list of commands agree, so the order cannot be changed in one place only.</summary>
    [TestMethod]
    public void OnOpen_IsMadeOfTheTwoDocumentedCommands()
    {
        Assert.AreEqual(SqlitePragmas.BusyTimeoutCommand, SqlitePragmas.OnOpen[0]);
        Assert.AreEqual(SqlitePragmas.WriteAheadLoggingCommand, SqlitePragmas.OnOpen[1]);
    }

    /// <summary>The script that is run when a connection opens is the two commands, one after the other, with the busy timeout first.</summary>
    [TestMethod]
    public void OnOpenScript_IsTheTwoCommandsInOrder()
    {
        string joined = string.Join(' ', SqlitePragmas.OnOpen);

        Assert.AreEqual(SqlitePragmas.OnOpenScript, joined);
        Assert.IsLessThan(
            SqlitePragmas.OnOpenScript.IndexOf("journal_mode", System.StringComparison.Ordinal),
            SqlitePragmas.OnOpenScript.IndexOf("busy_timeout", System.StringComparison.Ordinal));
    }

    /// <summary>The wait is five seconds, in milliseconds for the pragma and in seconds for the provider, and both agree.</summary>
    [TestMethod]
    public void BusyTimeout_IsFiveSecondsInBothUnits()
    {
        int seconds = new SqliteConnectionStringBuilder(SqlitePragmas.WithCommandTimeout("Data Source=expensehub.db")).DefaultTimeout;

        Assert.AreEqual(5000, seconds * 1000);
        Assert.Contains("5000", SqlitePragmas.BusyTimeoutCommand);
    }

    /// <summary>A connection string without a command timeout gets the same wait, and keeps its file.</summary>
    [TestMethod]
    public void WithCommandTimeout_AddsTheTimeoutWhenThereIsNone()
    {
        string result = SqlitePragmas.WithCommandTimeout("Data Source=expensehub.db");

        var builder = new SqliteConnectionStringBuilder(result);
        Assert.AreEqual(5, builder.DefaultTimeout);
        Assert.AreEqual("expensehub.db", builder.DataSource);
    }

    /// <summary>A command timeout chosen in the connection string is kept, under either of its names.</summary>
    /// <param name="connectionString">The connection string.</param>
    [TestMethod]
    [DataRow("Data Source=expensehub.db;Default Timeout=12")]
    [DataRow("Data Source=expensehub.db;Command Timeout=12")]
    public void WithCommandTimeout_KeepsATimeoutThatWasChosen(string connectionString)
    {
        string result = SqlitePragmas.WithCommandTimeout(connectionString);

        Assert.AreEqual(12, new SqliteConnectionStringBuilder(result).DefaultTimeout);
    }

    /// <summary>Only the timeout is added: the file, the mode and the rest of the connection string stay as they were.</summary>
    [TestMethod]
    public void WithCommandTimeout_ChangesNothingElse()
    {
        string original = "Data Source=/data/expensehub.db;Mode=ReadWriteCreate;Cache=Shared";

        var result = new SqliteConnectionStringBuilder(SqlitePragmas.WithCommandTimeout(original));

        Assert.AreEqual("/data/expensehub.db", result.DataSource);
        Assert.AreEqual(SqliteOpenMode.ReadWriteCreate, result.Mode);
        Assert.AreEqual(SqliteCacheMode.Shared, result.Cache);
    }
}
