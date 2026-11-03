namespace ExpenseHub.Api.Reports;

/// <summary>
/// A validation problem of one field of a report request.
/// </summary>
/// <param name="Field">The name of the field.</param>
/// <param name="Message">The message that explains the problem.</param>
public sealed record ReportValidationError(string Field, string Message);
