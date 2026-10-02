namespace ExpenseHub.Api.Identity;

/// <summary>
/// Configuration of the initial Admin account. The password is never versioned:
/// it comes from user-secrets or from the <c>Seed__Admin__Password</c> environment variable.
/// </summary>
public sealed class AdminSeedOptions
{
    /// <summary>The configuration section that holds these options.</summary>
    public const string SectionName = "Seed:Admin";

    /// <summary>Gets or sets the e-mail address (and user name) of the initial Admin.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the password of the initial Admin.</summary>
    public string? Password { get; set; }
}
