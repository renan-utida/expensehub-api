using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Creation and edition of expense drafts. Only users in the <c>Employee</c> role can reach it.
/// </summary>
[ApiController]
[Route("api/expenses")]
[Authorize(Roles = AppRoles.Employee)]
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
    /// <c>201</c> with the draft; <c>400</c> for invalid data; <c>401</c> without a token;
    /// <c>403</c> without the Employee role.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ExpenseRequest request)
    {
        ExpenseOperationResult result = await _expenses.CreateAsync(GetUserId(), ToDetails(request));

        return ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Replaces the description, amount and date of a draft that belongs to the authenticated user.
    /// </summary>
    /// <param name="id">The identifier of the expense.</param>
    /// <param name="request">The new description, amount and date.</param>
    /// <returns>
    /// <c>200</c> with the draft; <c>400</c> for invalid data; <c>404</c> when the expense does not exist or belongs to another user;
    /// <c>409</c> when the expense is not a draft.
    /// </returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] ExpenseRequest request)
    {
        ExpenseOperationResult result = await _expenses.UpdateAsync(GetUserId(), id, ToDetails(request));

        return ToActionResult(result, StatusCodes.Status200OK);
    }

    private static ExpenseDetails ToDetails(ExpenseRequest request)
    {
        return new ExpenseDetails(request.Description, request.Amount, request.ExpenseDate);
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The token has no user identifier.");
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
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Expense not found.");

            default:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Only a draft expense can be edited.");
        }
    }
}
