using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Hand-written <see cref="ProblemDetailsFactory"/> for the controller tests. The controller asks the request services for
/// this factory when it builds a problem response; a test has no host, so it hands the controller this one instead.
/// It keeps the status, the title and the validation errors it is given and adds nothing else.
/// </summary>
internal sealed class FakeProblemDetailsFactory : ProblemDetailsFactory
{
    /// <inheritdoc />
    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        return new ProblemDetails
        {
            Status = statusCode ?? StatusCodes.Status500InternalServerError,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance,
        };
    }

    /// <inheritdoc />
    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        return new ValidationProblemDetails(modelStateDictionary)
        {
            Status = statusCode ?? StatusCodes.Status400BadRequest,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance,
        };
    }
}
