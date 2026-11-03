using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// The settings that let the web application and the worker share one SQLite file. Both processes build their context through
/// <c>AddExpenseHubPersistence</c>, so both get the same settings, from this one place:
/// <list type="bullet">
/// <item>The busy timeout makes a connection wait for a lock held by the other process instead of failing at once with
/// "database is locked".</item>
/// <item>The write-ahead log lets one process read while the other writes. The mode is stored in the database file, so it is set
/// by whichever process opens the file first.</item>
/// </list>
/// The busy timeout is set <b>before</b> the journal mode on purpose: changing to the write-ahead log needs a lock, and if the other
/// process opens the file at the same time, the connection must already know how long to wait for it.
/// </summary>
public static class SqlitePragmas
{
    /// <summary>How long a connection waits for a lock of the other process, in milliseconds.</summary>
    public const int BusyTimeoutMilliseconds = 5000;

    /// <summary>The same wait, in seconds, as the command timeout of the provider.</summary>
    public const int BusyTimeoutSeconds = 5;

    /// <summary>The command that sets the wait for a lock. It must come before <see cref="WriteAheadLoggingCommand"/>.</summary>
    public const string BusyTimeoutCommand = "PRAGMA busy_timeout = 5000;";

    /// <summary>The command that switches the database to the write-ahead log.</summary>
    public const string WriteAheadLoggingCommand = "PRAGMA journal_mode = WAL;";

    /// <summary>
    /// The two commands as one script, in order, run every time a connection is opened. It is a constant, so the text that reaches
    /// the database is never built at run time.
    /// </summary>
    public const string OnOpenScript = BusyTimeoutCommand + " " + WriteAheadLoggingCommand;

    /// <summary>Gets the commands to run, in this order, every time a connection is opened.</summary>
    public static IReadOnlyList<string> OnOpen { get; } = [BusyTimeoutCommand, WriteAheadLoggingCommand];

    /// <summary>
    /// Sets the command timeout of the provider to the same wait, unless the connection string already chose one. The provider
    /// retries a busy statement until the command timeout is over, so this is what limits the wait of a statement.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The connection string with a command timeout.</returns>
    public static string WithCommandTimeout(string connectionString)
    {
        // The generic builder only holds the keys that were written, so it can tell a chosen timeout from the default one.
        var written = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (written.ContainsKey("Default Timeout") || written.ContainsKey("Command Timeout"))
        {
            return connectionString;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            DefaultTimeout = BusyTimeoutSeconds,
        };

        return builder.ConnectionString;
    }
}
