namespace ExpenseHub.Api.Persistence.Repositories;

/// <summary>
/// Tells the constraint failures of SQLite apart by their result codes. It works on plain numbers, with no provider
/// types, so the rule can be tested without a database.
/// </summary>
public static class SqliteConstraintErrors
{
    /// <summary>The result code SQLite gives to every constraint failure.</summary>
    private const int ConstraintErrorCode = 19;

    /// <summary>The extended result code SQLite gives when a unique index rejects a row.</summary>
    private const int UniqueConstraintExtendedErrorCode = 2067;

    /// <summary>
    /// Checks whether a failure was caused by a unique index. A failure of a foreign key, of a check or of a trigger,
    /// or a busy database, is not a unique violation.
    /// </summary>
    /// <param name="errorCode">The result code of SQLite.</param>
    /// <param name="extendedErrorCode">The extended result code of SQLite.</param>
    /// <returns><c>true</c> when a unique index rejected the row.</returns>
    public static bool IsUniqueConstraint(int errorCode, int extendedErrorCode)
    {
        return errorCode == ConstraintErrorCode && extendedErrorCode == UniqueConstraintExtendedErrorCode;
    }
}
