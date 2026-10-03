using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Creation, edition and submission of expense drafts, and reading of expenses by profile.
/// Writing needs the <c>Employee</c> role; reading needs one of the roles that can read expenses.
/// <c>Admin</c> alone gives no access.
/// </summary>
[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenses;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpensesController"/> class.
    /// </summary>
    /// <param name="expenses">The expense service.</param>
    public ExpensesController(ExpenseService expenses)
    {
        _expenses = expenses;
    }

    /// <summary>
    /// Creates a draft. The owner is the authenticated user and the state is always <c>Draft</c>.
    /// </summary>
    /// <param name="request">The description, amount and date of the expense.</param>
    /// <returns>
    /// <c>201</c> with the draft and its <c>Location</c>; <c>400</c> for invalid data; <c>401</c> without a token;
    /// <c>403</c> without the Employee role.
    /// </returns>
    [HttpPost]
    [Authorize(Roles = AppRoles.Employee)]
    public async Task<IActionResult> CreateAsync([FromBody] ExpenseRequest request)
    {
        ExpenseOperationResult result = await _expenses.CreateAsync(GetCaller(), ToDetails(request));

        if (result.Status == ExpenseOperationStatus.Succeeded)
        {
            ExpenseResponse created = ExpenseResponse.From(result.Expense!);

            return Created($"/api/expenses/{created.Id}", created);
        }

        return ToActionResult(result, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Replaces the description, amount and date of a draft that belongs to the authenticated user.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <param name="request">The new description, amount and date.</param>
    /// <returns>
    /// <c>200</c> with the draft; <c>400</c> for invalid data; <c>404</c> when the expense does not exist or is outside the read scope of the user;
    /// <c>403</c> when the user can see the expense but does not own it; <c>409</c> when the expense is not a draft.
    /// </returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Employee)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] ExpenseRequest request)
    {
        ExpenseOperationResult result = await _expenses.UpdateAsync(GetCaller(), id, ToDetails(request));

        return ToActionResult(result, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Submits a draft of the authenticated user: <c>Draft</c> to <c>Submitted</c>. The state is never sent by the client.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <returns>
    /// <c>200</c> with the submitted expense; <c>404</c> when the expense does not exist or is outside the read scope of the user;
    /// <c>403</c> when the user can see the expense but does not own it; <c>409</c> when the expense is not a draft
    /// (including a repeated submit); <c>401</c> without a token; <c>403</c> without the Employee role.
    /// </returns>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = AppRoles.Employee)]
    public async Task<IActionResult> SubmitAsync(Guid id)
    {
        ExpenseOperationResult result = await _expenses.SubmitAsync(GetCaller(), id);

        return ToActionResult(result, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Lists the expenses the authenticated user can read, newest first. The filter of the profile is applied inside the query:
    /// Employee reads its own, Approver the submitted ones, Finance the approved and paid ones, and Auditor all of them.
    /// </summary>
    /// <returns>
    /// <c>200</c> with the visible expenses; <c>401</c> without a token; <c>403</c> for a user whose roles do not read expenses,
    /// such as an Admin alone.
    /// </returns>
    [HttpGet]
    [Authorize(Roles = AppRoles.ExpenseReaders)]
    public async Task<ActionResult<IReadOnlyList<ExpenseResponse>>> ListAsync()
    {
        IReadOnlyList<Expense> expenses = await _expenses.ListAsync(GetCaller());

        return expenses.Select(ExpenseResponse.From).ToList();
    }

    /// <summary>
    /// Gets one expense the authenticated user can read. An expense that does not exist and one outside the read
    /// scope of the user get the same <c>404</c>, so nothing leaks about expenses the user cannot see.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <returns>
    /// <c>200</c> with the expense; <c>404</c> when it does not exist or is not visible; <c>401</c> without a token;
    /// <c>403</c> for a user whose roles do not read expenses.
    /// </returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.ExpenseReaders)]
    public async Task<ActionResult<ExpenseResponse>> GetAsync(Guid id)
    {
        Expense? expense = await _expenses.GetAsync(GetCaller(), id);

        return expense is null ? ExpenseNotFound() : ExpenseResponse.From(expense);
    }

    private static ExpenseDetails ToDetails(ExpenseRequest request)
    {
        return new ExpenseDetails(request.Description, request.Amount, request.ExpenseDate);
    }

    private ExpenseCaller GetCaller()
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The token has no user identifier.");
        string[] roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        return new ExpenseCaller(userId, roles);
    }

    private ObjectResult ExpenseNotFound()
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Expense not found.");
    }

    private IActionResult ToActionResult(ExpenseOperationResult result, int successStatusCode)
    {
        switch (result.Status)
        {
            case ExpenseOperationStatus.Succeeded:
                return StatusCode(successStatusCode, ExpenseResponse.From(result.Expense!));

            case ExpenseOperationStatus.ValidationFailed:
                foreach (ExpenseValidationError error in result.Errors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                return ValidationProblem(ModelState);

            case ExpenseOperationStatus.NotFound:
                return ExpenseNotFound();

            case ExpenseOperationStatus.Forbidden:
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "You are not allowed to do this with this expense.");

            default:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The expense is not a draft.");
        }
    }
}
