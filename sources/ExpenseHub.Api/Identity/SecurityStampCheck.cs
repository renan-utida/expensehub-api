using System;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Decides whether the security stamp carried by a token is still the current one.
/// </summary>
public static class SecurityStampCheck
{
    /// <summary>
    /// A token is current only when it carries a stamp and that stamp equals the one stored for the user.
    /// A missing user (no current stamp) or a missing claim makes the token stale.
    /// </summary>
    /// <param name="tokenStamp">The stamp carried by the token.</param>
    /// <param name="currentStamp">The stamp currently stored for the user.</param>
    /// <returns><c>true</c> when the token can still be used.</returns>
    public static bool IsCurrent(string? tokenStamp, string? currentStamp)
    {
        return !string.IsNullOrEmpty(tokenStamp)
            && !string.IsNullOrEmpty(currentStamp)
            && string.Equals(tokenStamp, currentStamp, StringComparison.Ordinal);
    }
}
