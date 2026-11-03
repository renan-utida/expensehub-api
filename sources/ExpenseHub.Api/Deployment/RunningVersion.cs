namespace ExpenseHub.Api.Deployment;

/// <summary>
/// Turns the commit that the configuration reports into a value that is safe to write in a log line: only a hexadecimal commit
/// id is kept, anything else becomes <see cref="Unknown"/>. The commit comes from an environment variable, so it is never logged
/// as free text.
/// </summary>
public static class RunningVersion
{
    /// <summary>The value used when the commit is missing or is not a commit id.</summary>
    public const string Unknown = "unknown";

    private const int MinLength = 7;
    private const int MaxLength = 64;

    /// <summary>
    /// Validates the commit id. A short id (7 characters) up to a full SHA-256 id (64 characters) is accepted; the letters are
    /// returned in lower case. White space is not trimmed: a value with a line break is not a commit id.
    /// </summary>
    /// <param name="commit">The commit as it was read from the configuration.</param>
    /// <returns>The commit in lower case, or <see cref="Unknown"/>.</returns>
    public static string Normalize(string? commit)
    {
        if (commit is null || commit.Length < MinLength || commit.Length > MaxLength)
        {
            return Unknown;
        }

        foreach (char character in commit)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return Unknown;
            }
        }

        return commit.ToLowerInvariant();
    }
}
