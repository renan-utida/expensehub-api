using System.Collections.Generic;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// A user with the roles it currently has.
/// </summary>
/// <param name="Id">The identifier of the user.</param>
/// <param name="Email">The e-mail address of the user.</param>
/// <param name="Roles">The names of the roles of the user.</param>
public sealed record UserAccount(string Id, string? Email, IReadOnlyList<string> Roles);
