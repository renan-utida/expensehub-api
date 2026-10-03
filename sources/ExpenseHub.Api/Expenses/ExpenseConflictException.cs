using System;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The expense changed while it was being saved (for example, it was submitted by a concurrent request),
/// so the change was not applied. It lets the service answer <c>409</c> without knowing the storage technology.
/// </summary>
public sealed class ExpenseConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ExpenseConflictException"/> class.</summary>
    public ExpenseConflictException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ExpenseConflictException"/> class.</summary>
    /// <param name="message">The message that explains the conflict.</param>
    public ExpenseConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ExpenseConflictException"/> class.</summary>
    /// <param name="message">The message that explains the conflict.</param>
    /// <param name="innerException">The exception of the storage.</param>
    public ExpenseConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
