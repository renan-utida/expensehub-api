using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence.Repositories;

/// <summary>
/// Tells the transient failures of SQLite from the others. A transient failure says nothing about the data: the database is busy
/// or locked by another process (the web application and the worker share the file), a read or write of the disk failed, the disk
/// is full, memory ran out, or the file cannot be opened right now. Waiting and trying again can fix it, so the worker does not
/// mark the report as failed and does not acknowledge the message. Anything else (a constraint, a malformed statement) is a problem
/// with the request, and retrying will not change it.
/// </summary>
public static class SqliteTransientErrors
{
    private const int PrimaryCodeMask = 0xFF;
    private const int Busy = 5;
    private const int Locked = 6;
    private const int NoMemory = 7;
    private const int InputOutput = 10;
    private const int Full = 13;
    private const int CannotOpen = 14;

    /// <summary>
    /// Checks a result code of SQLite. The extended codes (for example, a busy database waiting for recovery) keep the primary code
    /// in their low byte, so they are told apart too.
    /// </summary>
    /// <param name="resultCode">The result code or the extended result code.</param>
    /// <returns><c>true</c> when the code is a transient failure.</returns>
    public static bool IsTransient(int resultCode)
    {
        int primary = resultCode & PrimaryCodeMask;

        return primary is Busy or Locked or NoMemory or InputOutput or Full or CannotOpen;
    }

    /// <summary>
    /// Checks an exception thrown by the database access: the failure of the provider itself, or the same failure wrapped by
    /// the update of Entity Framework Core.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> when the exception is a transient failure of SQLite.</returns>
    public static bool IsTransient(Exception? exception)
    {
        return exception switch
        {
            SqliteException sqlite => IsTransient(sqlite.SqliteExtendedErrorCode != 0 ? sqlite.SqliteExtendedErrorCode : sqlite.SqliteErrorCode),
            DbUpdateException { InnerException: not null } update => IsTransient(update.InnerException),
            _ => false,
        };
    }
}
