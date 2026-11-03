using System;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The fields of a report request that the client controls. Everything else (requester, state, identifier and instants)
/// is set by the server. The members are nullable and the scope is still text, so the service checks them even when it is
/// not called by the API.
/// </summary>
/// <param name="FromDate">The first day of the period, inclusive.</param>
/// <param name="ToDate">The last day of the period, inclusive.</param>
/// <param name="Scope">The name of the scope: <c>Mine</c> or <c>Global</c>.</param>
public sealed record ReportRequestDetails(DateOnly? FromDate, DateOnly? ToDate, string? Scope);
