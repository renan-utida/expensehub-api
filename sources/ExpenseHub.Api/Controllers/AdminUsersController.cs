using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Administrative access to users and their roles. Only users in the <c>Admin</c> role can reach it.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserAccountService _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminUsersController"/> class.
    /// </summary>
    /// <param name="accounts">The user account service.</param>
    public AdminUsersController(UserAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>
    /// Lists the users with their roles, without password hashes or security data.
    /// </summary>
    /// <returns>The users ordered by e-mail; <c>401</c> without a token and <c>403</c> without the Admin role.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> ListAsync()
    {
        IReadOnlyList<UserAccount> users = await _accounts.ListAsync();

        return users.Select(UserSummaryResponse.From).ToList();
    }

    /// <summary>
    /// Replaces the roles of a user. The listed roles are granted and the ones left out are removed.
    /// Only known roles are accepted, and an Admin cannot remove their own Admin role.
    /// The affected user must log in again to get a token with the new roles; tokens issued before the change stop working.
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="request">The roles the user must end up with.</param>
    /// <returns>
    /// <c>200</c> with the user; <c>400</c> for an invalid or unknown role; <c>403</c> when an Admin removes their own Admin role;
    /// <c>404</c> when the user does not exist.
    /// </returns>
    [HttpPut("{id}/roles")]
    public async Task<ActionResult<UserSummaryResponse>> UpdateRolesAsync(string id, [FromBody] UpdateUserRolesRequest request)
    {
        string? actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        UpdateRolesResult result = await _accounts.UpdateRolesAsync(actorId, id, request.Roles);

        switch (result.Status)
        {
            case UpdateRolesStatus.InvalidRoles:
                ModelState.AddModelError(nameof(UpdateUserRolesRequest.Roles), $"Unknown role(s): {string.Join(", ", result.InvalidRoles)}.");
                return ValidationProblem(ModelState);

            case UpdateRolesStatus.CannotRemoveOwnAdminRole:
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "An Admin cannot remove their own Admin role.");

            case UpdateRolesStatus.UserNotFound:
                return UserNotFound();

            default:
                return result.User is null ? UserNotFound() : UserSummaryResponse.From(result.User);
        }
    }

    private ObjectResult UserNotFound()
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "User not found.");
    }
}
