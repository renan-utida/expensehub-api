using System;
using Microsoft.AspNetCore.Builder;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Pipeline registration of <see cref="SecurityStampValidationMiddleware"/>.
/// </summary>
public static class SecurityStampValidationApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the security stamp check. Call it after <c>UseAuthentication</c> and before <c>UseAuthorization</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseSecurityStampValidation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<SecurityStampValidationMiddleware>();
    }
}
