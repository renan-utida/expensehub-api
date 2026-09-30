using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Authenticates users and issues bearer tokens.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signInManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="signInManager">The Identity sign-in manager.</param>
    public AuthController(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    /// <summary>
    /// Validates the credentials and, when valid, writes the bearer token to the response.
    /// Unknown e-mail, wrong password and locked-out accounts all answer the same <c>401</c>, so the response does not reveal which accounts exist.
    /// </summary>
    /// <param name="request">The credentials.</param>
    /// <returns>The bearer token on success; <c>400</c> for invalid input or <c>401</c> for invalid credentials.</returns>
    [HttpPost("/login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityUser? user = await _signInManager.UserManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidCredentials();
        }

        _signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

        IdentitySignInResult result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        return result.Succeeded ? new EmptyResult() : InvalidCredentials();
    }

    private ObjectResult InvalidCredentials()
    {
        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials.");
    }
}
