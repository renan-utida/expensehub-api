using System.Collections.Generic;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// The possible outcomes of registering a user.
/// </summary>
public enum RegistrationStatus
{
    /// <summary>The user was created, without any role.</summary>
    Created,

    /// <summary>A user with the same e-mail address already exists.</summary>
    DuplicateEmail,

    /// <summary>The e-mail address or the password was rejected by the Identity rules.</summary>
    Invalid,
}

/// <summary>
/// The result of registering a user.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="User">The created user, when the status is <see cref="RegistrationStatus.Created"/>.</param>
/// <param name="ErrorCodes">The Identity error codes, when the status is <see cref="RegistrationStatus.Invalid"/>. Never the password.</param>
public sealed record RegistrationResult(RegistrationStatus Status, UserAccount? User, IReadOnlyList<string> ErrorCodes);
