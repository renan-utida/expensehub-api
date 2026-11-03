using System;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The message of a report could not be handed to the broker (for example, the broker is down). The service answers as for a
/// message the broker did not confirm, without knowing the messaging technology.
/// </summary>
public sealed class ReportPublishException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ReportPublishException"/> class.</summary>
    public ReportPublishException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReportPublishException"/> class.</summary>
    /// <param name="message">The message that explains the failure.</param>
    public ReportPublishException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReportPublishException"/> class.</summary>
    /// <param name="message">The message that explains the failure.</param>
    /// <param name="innerException">The exception of the broker client.</param>
    public ReportPublishException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
