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
/// Registers users and authenticates them, issuing bearer tokens.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserAccountService _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="signInManager">The Identity sign-in manager.</param>
    /// <param name="accounts">The user account service.</param>
    public AuthController(SignInManager<IdentityUser> signInManager, UserAccountService accounts)
    {
        _signInManager = signInManager;
        _accounts = accounts;
    }

    /// <summary>
    /// Registers a new user. The user is created without any role, and any role sent by the client is ignored:
    /// only an Admin can grant roles afterwards. Registering does not log the user in.
    /// </summary>
    /// <param name="request">The e-mail address and password of the new user.</param>
    /// <returns>
    /// <c>201</c> with the new user; <c>400</c> for invalid input or a password that breaks the Identity policy;
    /// <c>409</c> when the e-mail address is already registered.
    /// </returns>
    [HttpPost("/register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RegistrationResult result = await _accounts.RegisterAsync(request.Email, request.Password);

        switch (result.Status)
        {
            case RegistrationStatus.Created:
                return StatusCode(StatusCodes.Status201Created, UserSummaryResponse.From(result.User!));

            case RegistrationStatus.DuplicateEmail:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "E-mail already registered.");

            default:
                foreach (string code in result.ErrorCodes)
                {
                    string field = code.StartsWith("Password", StringComparison.Ordinal)
                        ? nameof(RegisterRequest.Password)
                        : nameof(RegisterRequest.Email);
                    ModelState.AddModelError(field, code);
                }

                return ValidationProblem(ModelState);
        }
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
