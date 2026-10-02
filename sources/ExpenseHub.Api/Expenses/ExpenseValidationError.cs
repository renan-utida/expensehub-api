namespace ExpenseHub.Api.Expenses;

/// <summary>
/// A validation problem of one field of an expense.
/// </summary>
/// <param name="Field">The name of the field.</param>
/// <param name="Message">The message that explains the problem.</param>
public sealed record ExpenseValidationError(string Field, string Message);
