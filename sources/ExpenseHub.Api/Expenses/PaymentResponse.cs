using System;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Public representation of a payment: the expense that was paid, its new state, who paid and when. The actor and the
/// instant are the ones the server recorded in the payment record, never data sent by the client.
/// </summary>
/// <param name="ExpenseId">The identifier of the paid expense.</param>
/// <param name="Status">The state of the expense, as text.</param>
/// <param name="ActorId">The identifier of the user who paid, taken from the token.</param>
/// <param name="PaidAtUtc">The UTC instant of the payment, set by the server.</param>
public sealed record PaymentResponse(
    Guid ExpenseId,
    string Status,
    string ActorId,
    DateTimeOffset PaidAtUtc)
{
    /// <summary>
    /// Builds the response of the payment of an expense.
    /// </summary>
    /// <param name="expense">The paid expense, with its payment record.</param>
    /// <returns>The public representation.</returns>
    /// <exception cref="InvalidOperationException">The expense has no payment record.</exception>
    public static PaymentResponse From(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        PaymentRecord payment = expense.Payment
            ?? throw new InvalidOperationException("The expense has no payment record.");

        return new PaymentResponse(expense.Id, expense.Status.ToString(), payment.ActorId, payment.PaidAtUtc);
    }
}
