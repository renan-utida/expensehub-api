using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Data sent to <c>POST /register</c>. It has no role member on purpose: registration never accepts roles,
/// and any role sent by the client is ignored.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>Gets the e-mail address of the new user.</summary>
    [Required]
    [EmailAddress]
    [StringLength(LoginRequest.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Gets the password of the new user. The Identity password policy still applies.</summary>
    [Required]
    [StringLength(LoginRequest.PasswordMaxLength)]
    public string Password { get; init; } = string.Empty;
}
