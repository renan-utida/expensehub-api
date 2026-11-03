namespace ExpenseHub.Api.Reports;

/// <summary>
/// The stable codes and the fixed messages of a report that failed. The code is what a client can rely on; the message is
/// fixed text, never built from an exception, so no path, connection string, identifier or stack trace can reach the response.
/// </summary>
public static class ReportErrorCodes
{
    /// <summary>The saved request is not coherent (for example, it has no requester), so it cannot be calculated.</summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>The calculation failed for a reason that retrying cannot fix.</summary>
    public const string ProcessingFailed = "ProcessingFailed";

    /// <summary>The sanitized message that goes with <see cref="InvalidRequest"/>.</summary>
    public const string InvalidRequestMessage = "The saved report request is not valid.";

    /// <summary>The sanitized message that goes with <see cref="ProcessingFailed"/>.</summary>
    public const string ProcessingFailedMessage = "The report could not be processed.";
}
