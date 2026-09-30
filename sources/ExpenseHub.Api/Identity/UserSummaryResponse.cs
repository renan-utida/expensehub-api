namespace ExpenseHub.Api.Identity;

/// <summary>
/// Public summary of a user returned by the admin endpoints.
/// </summary>
/// <param name="Id">The identifier of the user.</param>
/// <param name="Email">The e-mail address of the user.</param>
public sealed record UserSummaryResponse(string Id, string? Email);
