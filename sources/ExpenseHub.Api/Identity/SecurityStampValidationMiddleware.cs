using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Rejects bearer tokens issued before the user's security stamp changed. The bearer token keeps the roles
/// it was issued with and is not checked against the database by itself, so without this check a role removed
/// by an Admin would keep working until the token expires. A stale token is treated as anonymous and the
/// authorization step answers <c>401</c>.
/// </summary>
public sealed class SecurityStampValidationMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityStampValidationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next step of the pipeline.</param>
    public SecurityStampValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Compares the stamp of the authenticated token with the stored one.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="identityOptions">The Identity options, which name the stamp claim.</param>
    /// <returns>A task that completes when the rest of the pipeline is done.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        UserManager<IdentityUser> userManager,
        IOptions<IdentityOptions> identityOptions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(identityOptions);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? tokenStamp = context.User.FindFirstValue(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
            string? currentStamp = await FindCurrentStampAsync(userManager, userId);

            if (!SecurityStampCheck.IsCurrent(tokenStamp, currentStamp))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }

        await _next(context);
    }

    private static async Task<string?> FindCurrentStampAsync(UserManager<IdentityUser> userManager, string? userId)
    {
        if (userId is null)
        {
            return null;
        }

        IdentityUser? user = await userManager.FindByIdAsync(userId);

        return user is null ? null : await userManager.GetSecurityStampAsync(user);
    }
}
