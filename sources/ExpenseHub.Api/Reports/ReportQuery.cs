using System;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// What a report adds up: the period, the scope and who asked. The worker builds it from the saved request,
/// never from the message. The requester is always required, so the scope <c>Mine</c> can never fall back to everyone.
/// </summary>
public sealed class ReportQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQuery"/> class.
    /// </summary>
    /// <param name="scope">Whose expenses the report adds up.</param>
    /// <param name="requesterId">The identifier of the user who asked for the report.</param>
    /// <param name="fromDate">The first day of the period, inclusive.</param>
    /// <param name="toDate">The last day of the period, inclusive.</param>
    /// <exception cref="ArgumentException">The requester is blank, or the period ends before it starts.</exception>
    public ReportQuery(ReportScope scope, string requesterId, DateOnly fromDate, DateOnly toDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

        if (fromDate > toDate)
        {
            throw new ArgumentException("The period cannot end before it starts.", nameof(fromDate));
        }

        Scope = scope;
        RequesterId = requesterId;
        FromDate = fromDate;
        ToDate = toDate;
    }

    /// <summary>Gets whose expenses the report adds up.</summary>
    public ReportScope Scope { get; }

    /// <summary>Gets the identifier of the user who asked for the report.</summary>
    public string RequesterId { get; }

    /// <summary>Gets the first day of the period, inclusive.</summary>
    public DateOnly FromDate { get; }

    /// <summary>Gets the last day of the period, inclusive.</summary>
    public DateOnly ToDate { get; }

    /// <summary>
    /// Builds the query of a report from the request that was saved. This is the only way the worker gets the scope, the
    /// requester and the period: it never reads the roles of the user again and never trusts the content of the message,
    /// which carries only the identifier. A <c>Mine</c> report therefore adds up the expenses of the requester that was
    /// recorded when the request was accepted, and nobody else's.
    /// </summary>
    /// <param name="report">The saved request.</param>
    /// <returns>The query, with the scope, the requester and the period of the saved request.</returns>
    public static ReportQuery From(ReportRequest report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ReportQuery(report.Scope, report.RequesterId, report.FromDate, report.ToDate);
    }
}
