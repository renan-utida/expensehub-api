using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Storage operations the report service needs, kept behind an interface so the rules can be tested without a database.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Stores a new report request.
    /// </summary>
    /// <param name="report">The request, in the state <c>Pending</c>.</param>
    /// <exception cref="ReportStorageException">The storage could not save the request, so nothing was saved.</exception>
    /// <returns>A task that completes when the request is saved.</returns>
    Task AddAsync(ReportRequest report);

    /// <summary>
    /// Lists the requests made by one user, newest first. The requester is part of the query, so a request of someone else is
    /// never loaded.
    /// </summary>
    /// <param name="requesterId">The identifier of the user.</param>
    /// <returns>The requests of the user.</returns>
    Task<IReadOnlyList<ReportRequest>> ListByRequesterAsync(string requesterId);

    /// <summary>
    /// Finds a request made by one user. The requester is part of the query, so a request of someone else looks the same as
    /// one that does not exist.
    /// </summary>
    /// <param name="id">The identifier of the request.</param>
    /// <param name="requesterId">The identifier of the user.</param>
    /// <returns>The request, or <c>null</c> when it does not exist or belongs to someone else.</returns>
    Task<ReportRequest?> FindByRequesterAsync(Guid id, string requesterId);

    /// <summary>
    /// Reads the state and the amount of the expenses that belong to a report, as they are now. The period and the scope
    /// are part of the query, so only those expenses are loaded. It only reads: no expense is tracked or changed.
    /// The worker uses it to calculate a report.
    /// </summary>
    /// <param name="query">The period, the scope and the requester, taken from the saved request.</param>
    /// <returns>One line for each expense of the report.</returns>
    Task<IReadOnlyList<ReportExpenseLine>> ListExpenseLinesAsync(ReportQuery query);
}
