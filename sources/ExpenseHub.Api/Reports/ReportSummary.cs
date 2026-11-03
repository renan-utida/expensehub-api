using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// How many expenses are in one state, and how much they add up to.
/// </summary>
/// <param name="Status">The state of the expenses.</param>
/// <param name="Count">The number of expenses in the state.</param>
/// <param name="Amount">The sum of their amounts, as a decimal.</param>
public sealed record ReportStatusTotal(ExpenseStatus Status, int Count, decimal Amount);

/// <summary>
/// The result of a report: the total and the five states, always all present, even with zeros.
/// </summary>
/// <param name="GeneratedAtUtc">The UTC instant the result was calculated.</param>
/// <param name="TotalCount">The number of expenses of all the states.</param>
/// <param name="TotalAmount">The sum of the amounts of all the states, as a decimal.</param>
/// <param name="ByStatus">The five states, in the order of <see cref="ExpenseStatus"/>.</param>
public sealed record ReportSummary(
    DateTimeOffset GeneratedAtUtc,
    int TotalCount,
    decimal TotalAmount,
    IReadOnlyList<ReportStatusTotal> ByStatus);
