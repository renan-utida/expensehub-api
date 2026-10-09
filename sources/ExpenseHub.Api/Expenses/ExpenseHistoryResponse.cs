using System;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Public representation of one entry in the history of an expense. It has only internal identifiers, never the e-mail
/// of the actor, and the states and the action are written as text.
/// </summary>
/// <param name="Id">The identifier of the entry.</param>
/// <param name="ExpenseId">The identifier of the expense.</param>
/// <param name="Action">What was done, as text.</param>
/// <param name="ActorId">The identifier of the user who did it, taken from the token when it happened.</param>
/// <param name="OccurredAtUtc">The UTC instant, set by the server.</param>
/// <param name="PreviousStatus">The state before the action, as text; <c>null</c> when the expense did not exist yet.</param>
/// <param name="NewStatus">The state after the action, as text.</param>
/// <param name="Reason">The reason of the rejection, when the action is a rejection.</param>
/// <param name="Changes">The summary of what changed, when the action is an edition of a draft.</param>
public sealed record ExpenseHistoryResponse(
    int Id,
    Guid ExpenseId,
    string Action,
    string ActorId,
    DateTimeOffset OccurredAtUtc,
    string? PreviousStatus,
    string NewStatus,
    string? Reason,
    string? Changes)
{
    /// <summary>
    /// Builds the response of a history entry.
    /// </summary>
    /// <param name="entry">The history entry.</param>
    /// <returns>The public representation.</returns>
    public static ExpenseHistoryResponse From(ExpenseHistory entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ExpenseHistoryResponse(
            entry.Id,
            entry.ExpenseId,
            entry.Action.ToString(),
            entry.ActorId,
            entry.OccurredAtUtc,
            entry.PreviousStatus?.ToString(),
            entry.NewStatus.ToString(),
            entry.Reason,
            entry.Changes);
    }
}
