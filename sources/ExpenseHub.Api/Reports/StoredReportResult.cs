using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The shape of the result as it is saved in <c>ReportRequest.ResultJson</c>: money in integer cents, never decimals.
/// Only <see cref="ReportResultSerializer"/> writes and reads it.
/// </summary>
/// <param name="GeneratedAtUtc">The UTC instant the result was calculated.</param>
/// <param name="TotalCount">The number of expenses of all the states.</param>
/// <param name="TotalAmountCents">The sum of the amounts of all the states, in cents.</param>
/// <param name="ByStatus">The five states.</param>
internal sealed record StoredReportResult(
    DateTimeOffset GeneratedAtUtc,
    int TotalCount,
    long TotalAmountCents,
    IReadOnlyList<StoredReportStatusTotal> ByStatus);

/// <summary>
/// The count and the amount, in cents, of the expenses in one state, as they are saved.
/// </summary>
/// <param name="Status">The state of the expenses.</param>
/// <param name="Count">The number of expenses in the state.</param>
/// <param name="AmountCents">The sum of their amounts, in cents.</param>
internal sealed record StoredReportStatusTotal(ExpenseStatus Status, int Count, long AmountCents);
