using System;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// The fields of an expense that the client controls. Everything else (owner, state, actor and instants)
/// is set by the server. The members are nullable so the service can check them even when it is not called by the API.
/// </summary>
/// <param name="Description">The description of the expense.</param>
/// <param name="Amount">The single amount of the expense.</param>
/// <param name="ExpenseDate">The date the expense happened.</param>
public sealed record ExpenseDetails(string? Description, decimal? Amount, DateOnly? ExpenseDate);
