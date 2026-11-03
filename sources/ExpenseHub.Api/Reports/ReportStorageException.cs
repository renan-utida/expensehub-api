using System;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The storage is temporarily unavailable (for example, the database is busy, locked or cannot be opened). When a request could
/// not be saved, it lets the service answer with an explicit error, and without an identifier; when the worker meets it, the
/// message is not acknowledged and comes back later. Neither knows the storage technology.
/// </summary>
public sealed class ReportStorageException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ReportStorageException"/> class.</summary>
    public ReportStorageException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReportStorageException"/> class.</summary>
    /// <param name="message">The message that explains the failure.</param>
    public ReportStorageException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReportStorageException"/> class.</summary>
    /// <param name="message">The message that explains the failure.</param>
    /// <param name="innerException">The exception of the storage.</param>
    public ReportStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
