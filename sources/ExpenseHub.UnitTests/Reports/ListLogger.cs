using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Hand-written logger that keeps every line it receives, formatted as the real logger would write it, so a test can check what
/// was logged and what was not.
/// </summary>
/// <typeparam name="T">The category of the logger.</typeparam>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<LogLine> _lines = new();

    /// <summary>Gets the lines that were logged, in order.</summary>
    public IReadOnlyList<LogLine> Lines => _lines;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _lines.Add(new LogLine(logLevel, formatter(state, exception), exception));
    }
}

/// <summary>
/// One line that a <see cref="ListLogger{T}"/> received.
/// </summary>
/// <param name="Level">The level of the line.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception that was logged with the line, if any.</param>
internal sealed record LogLine(LogLevel Level, string Message, Exception? Exception);
