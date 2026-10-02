using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Credentials sent to <c>POST /login</c>.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>Maximum length of an e-mail address, matching the Identity column.</summary>
    public const int EmailMaxLength = 256;

    /// <summary>Maximum length of a password, to avoid hashing arbitrarily large input.</summary>
    public const int PasswordMaxLength = 128;

    /// <summary>Gets the e-mail address of the user.</summary>
    [Required]
    [EmailAddress]
    [StringLength(EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Gets the password of the user.</summary>
    [Required]
    [StringLength(PasswordMaxLength)]
    public string Password { get; init; } = string.Empty;
}
