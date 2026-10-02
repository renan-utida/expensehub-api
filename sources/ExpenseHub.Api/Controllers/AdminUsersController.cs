using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Administrative access to users. Only users in the <c>Admin</c> role can reach it.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminUsersController"/> class.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    public AdminUsersController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Lists the users, without password hashes or security data.
    /// </summary>
    /// <returns>The users ordered by e-mail; <c>401</c> without a token and <c>403</c> without the Admin role.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> ListAsync()
    {
        List<UserSummaryResponse> users = await _userManager.Users
            .OrderBy(user => user.Email)
            .Select(user => new UserSummaryResponse(user.Id, user.Email))
            .ToListAsync();

        return users;
    }
}
