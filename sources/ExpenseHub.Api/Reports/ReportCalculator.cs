using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Persistence.Converters;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The calculation of a report, in two read-only steps that depend on neither EF Core nor a database.
/// <see cref="Select"/> is the one rule of which expenses belong to a report, written as a query, so SQLite applies it
/// before any expense is loaded and the tests apply it to a list in memory. <see cref="Summarize"/> adds up in
/// integer cents and turns the sums into decimals only at the end, so no floating point error appears.
/// Nothing here changes an expense.
/// </summary>
public static class ReportCalculator
{
    /// <summary>
    /// Keeps the expenses of the report and reduces them to their state and amount. The period filters the date of the
    /// expense, with both limits inclusive, and never the date the expense was created. The scope <c>Mine</c> keeps only the
    /// expenses of the requester; the scope <c>Global</c> keeps the expenses of every owner.
    /// </summary>
    /// <param name="expenses">The expenses to choose from.</param>
    /// <param name="query">The period, the scope and the requester.</param>
    /// <returns>The query of the lines of the report; nothing runs until it is enumerated.</returns>
    public static IQueryable<ReportExpenseLine> Select(IQueryable<Expense> expenses, ReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(query);

        // The values are captured, not written into the expression, so the query is translated once for every report.
        DateOnly fromDate = query.FromDate;
        DateOnly toDate = query.ToDate;
        string requesterId = query.RequesterId;
        bool everyone = query.Scope == ReportScope.Global;

        return expenses
            .Where(expense => expense.ExpenseDate >= fromDate
                && expense.ExpenseDate <= toDate
                && (everyone || expense.OwnerId == requesterId))
            .Select(expense => new ReportExpenseLine(expense.Status, expense.Amount));
    }

    /// <summary>
    /// Adds up the lines by state. The five states are always in the result, in the order of <see cref="ExpenseStatus"/>,
    /// with zero when there is none; no line at all gives a result with every count and amount at zero.
    /// </summary>
    /// <param name="lines">The lines of the report.</param>
    /// <param name="generatedAtUtc">The UTC instant to record as the moment the result was calculated.</param>
    /// <returns>The summary, with decimal amounts.</returns>
    public static ReportSummary Summarize(IEnumerable<ReportExpenseLine> lines, DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(lines);

        ExpenseStatus[] statuses = Enum.GetValues<ExpenseStatus>();
        var counts = new Dictionary<ExpenseStatus, int>();
        var cents = new Dictionary<ExpenseStatus, long>();

        foreach (ExpenseStatus status in statuses)
        {
            counts[status] = 0;
            cents[status] = 0;
        }

        foreach (ReportExpenseLine line in lines)
        {
            counts[line.Status] = checked(counts[line.Status] + 1);
            cents[line.Status] = checked(cents[line.Status] + MoneyConversion.ToCents(line.Amount));
        }

        var byStatus = new List<ReportStatusTotal>();
        int totalCount = 0;
        long totalCents = 0;

        foreach (ExpenseStatus status in statuses)
        {
            byStatus.Add(new ReportStatusTotal(status, counts[status], MoneyConversion.FromCents(cents[status])));
            totalCount = checked(totalCount + counts[status]);
            totalCents = checked(totalCents + cents[status]);
        }

        return new ReportSummary(generatedAtUtc, totalCount, MoneyConversion.FromCents(totalCents), byStatus);
    }
}
