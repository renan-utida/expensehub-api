using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Data sent to <c>POST /api/expenses/{id}/reject</c>. It has only the reason of the rejection. There is no state, actor
/// or instant member on purpose, so the client cannot assign them: anything else in the body is ignored.
/// </summary>
public sealed class RejectExpenseRequest
{
    /// <summary>Gets the reason of the rejection, from 10 to 500 characters after the spaces at both ends are trimmed.</summary>
    [Required]
    [RejectionReason]
    public string? Reason { get; init; }
}
