using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The only two facts of an expense that a report needs: its state and its amount, as read while the report is processed.
/// </summary>
/// <param name="Status">The state of the expense when it was read.</param>
/// <param name="Amount">The amount of the expense when it was read.</param>
public sealed record ReportExpenseLine(ExpenseStatus Status, decimal Amount);
