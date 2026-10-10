using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Controllers;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of what the expenses controller does by itself, with the service replaced by a fake: which status code and title it
/// gives to each outcome of the service, what it passes to the service (the user from the claims, the identifier, the body,
/// the reason as it came) and which body it answers with. The rules of the service have their own tests.
/// </summary>
[TestClass]
public sealed class ExpensesControllerTests
{
    private const string UserId = "user-1";
    private const string Owner = "owner-1";

    /// <summary>A created draft is answered with 201, the Location of the new expense and the expense, and the controller passes the user of the claims and the fields of the body.</summary>
    [TestMethod]
    public async Task CreateAsync_Created_Returns201WithLocationAndForwardsTheCallerAndTheBody()
    {
        var service = new FakeExpenseService();
        Expense expense = ExpenseTestData.ExpenseOf(Owner);
        service.Result = ExpenseOperationResult.Success(expense);

        IActionResult result = await NewController(service, UserId, "Employee").CreateAsync(ValidRequest());

        CreatedResult created = Assert.IsInstanceOfType<CreatedResult>(result);
        Assert.AreEqual(StatusCodes.Status201Created, created.StatusCode);
        Assert.AreEqual($"/api/expenses/{expense.Id}", created.Location);
        Assert.AreEqual(expense.Id, Assert.IsInstanceOfType<ExpenseResponse>(created.Value).Id);
        CollectionAssert.AreEqual(new[] { nameof(ExpenseService.CreateAsync) }, service.Calls.ToArray());
        Assert.AreEqual(UserId, service.LastCaller!.UserId);
        Assert.IsTrue(service.LastCaller.IsInRole("Employee"));
        Assert.AreEqual(ValidRequest().Description, service.LastDetails!.Description);
        Assert.AreEqual(ValidRequest().Amount, service.LastDetails.Amount);
        Assert.AreEqual(ValidRequest().ExpenseDate, service.LastDetails.ExpenseDate);
    }

    /// <summary>Data the service finds invalid is answered with 400 and the error of each field.</summary>
    [TestMethod]
    public async Task CreateAsync_ServiceReportsInvalidData_Returns400WithTheFieldErrors()
    {
        var service = new FakeExpenseService
        {
            Result = ExpenseOperationResult.Invalid([new ExpenseValidationError("Amount", "The amount is required.")]),
        };

        IActionResult result = await NewController(service, UserId, "Employee").CreateAsync(ValidRequest());

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        ValidationProblemDetails problem = Assert.IsInstanceOfType<ValidationProblemDetails>(objectResult.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        string[] expectedErrors = ["The amount is required."];
        CollectionAssert.AreEqual(expectedErrors, problem.Errors["Amount"]);
    }

    /// <summary>The controller calls the method of its own action and no other, passes the identifier of the route and the user of the claims, and answers 200 with the expense.</summary>
    /// <param name="endpoint">The action.</param>
    /// <param name="serviceMethod">The only method of the service the action may call.</param>
    [TestMethod]
    [DataRow("update", nameof(ExpenseService.UpdateAsync))]
    [DataRow("submit", nameof(ExpenseService.SubmitAsync))]
    [DataRow("approve", nameof(ExpenseService.ApproveAsync))]
    [DataRow("reject", nameof(ExpenseService.RejectAsync))]
    public async Task Action_Succeeded_Returns200WithTheExpenseAndCallsOnlyItsOwnServiceMethod(string endpoint, string serviceMethod)
    {
        var service = new FakeExpenseService();
        Expense expense = ExpenseTestData.ExpenseOf(Owner);
        service.Result = ExpenseOperationResult.Success(expense);

        IActionResult result = await Invoke(NewController(service, UserId, "Employee", "Approver"), endpoint, expense.Id);

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status200OK, objectResult.StatusCode);
        Assert.AreEqual(expense.Id, Assert.IsInstanceOfType<ExpenseResponse>(objectResult.Value).Id);
        CollectionAssert.AreEqual(new[] { serviceMethod }, service.Calls.ToArray());
        Assert.AreEqual(expense.Id, service.LastExpenseId);
        Assert.AreEqual(UserId, service.LastCaller!.UserId);
        Assert.IsTrue(service.LastCaller.IsInRole("Employee"));
        Assert.IsTrue(service.LastCaller.IsInRole("Approver"));
    }

    /// <summary>Editing passes the fields of the body to the service, as they came.</summary>
    [TestMethod]
    public async Task UpdateAsync_ForwardsTheFieldsOfTheBody()
    {
        var service = new FakeExpenseService { Result = ExpenseOperationResult.Success(ExpenseTestData.ExpenseOf(Owner)) };
        ExpenseRequest request = ValidRequest();

        await NewController(service, UserId, "Employee").UpdateAsync(Guid.NewGuid(), request);

        Assert.AreEqual(request.Description, service.LastDetails!.Description);
        Assert.AreEqual(request.Amount, service.LastDetails.Amount);
        Assert.AreEqual(request.ExpenseDate, service.LastDetails.ExpenseDate);
    }

    /// <summary>Rejecting passes the reason to the service exactly as it came: trimming it is the job of the service, not of the controller.</summary>
    [TestMethod]
    public async Task RejectAsync_ForwardsTheReasonExactlyAsItCame()
    {
        var service = new FakeExpenseService { Result = ExpenseOperationResult.Success(ExpenseTestData.ExpenseOf(Owner)) };
        const string reason = "  Falta o comprovante da despesa  ";

        await NewController(service, UserId, "Approver").RejectAsync(Guid.NewGuid(), new RejectExpenseRequest { Reason = reason });

        Assert.AreEqual(reason, service.LastReason);
    }

    /// <summary>A reason the service finds invalid is answered with 400 and the error on the reason field.</summary>
    [TestMethod]
    public async Task RejectAsync_ServiceReportsInvalidReason_Returns400OnTheReasonField()
    {
        var service = new FakeExpenseService
        {
            Result = ExpenseOperationResult.Invalid([new ExpenseValidationError("Reason", "The reason is required.")]),
        };

        IActionResult result = await NewController(service, UserId, "Approver").RejectAsync(Guid.NewGuid(), new RejectExpenseRequest { Reason = "curta" });

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        ValidationProblemDetails problem = Assert.IsInstanceOfType<ValidationProblemDetails>(objectResult.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        Assert.IsTrue(problem.Errors.ContainsKey("Reason"));
    }

    /// <summary>A payment is answered with 200 and the payment (expense, state, who paid and when), and not with the expense.</summary>
    [TestMethod]
    public async Task PayAsync_Succeeded_Returns200WithThePaymentAndNotTheExpense()
    {
        var service = new FakeExpenseService();
        Expense expense = ExpenseTestData.ExpenseOf(Owner, ExpenseStatus.Paid);
        expense.Payment = new PaymentRecord { ActorId = "finance-1", PaidAtUtc = ExpenseTestData.Now };
        service.Result = ExpenseOperationResult.Success(expense);

        IActionResult result = await NewController(service, "finance-1", "Finance").PayAsync(expense.Id);

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status200OK, objectResult.StatusCode);
        PaymentResponse payment = Assert.IsInstanceOfType<PaymentResponse>(objectResult.Value);
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual("Paid", payment.Status);
        Assert.AreEqual("finance-1", payment.ActorId);
        Assert.AreEqual(ExpenseTestData.Now, payment.PaidAtUtc);
        CollectionAssert.AreEqual(new[] { nameof(ExpenseService.PayAsync) }, service.Calls.ToArray());
        Assert.AreEqual(expense.Id, service.LastExpenseId);
    }

    /// <summary>An expense that does not exist is a 404 and an expense of someone else, for an action that writes, is a 403, each with its own title.</summary>
    /// <param name="outcome">What the service answers.</param>
    /// <param name="expectedStatusCode">The status code the controller gives to it.</param>
    /// <param name="expectedTitle">The title of the problem.</param>
    [TestMethod]
    [DataRow(ExpenseOperationStatus.NotFound, StatusCodes.Status404NotFound, "Expense not found.")]
    [DataRow(ExpenseOperationStatus.Forbidden, StatusCodes.Status403Forbidden, "You are not allowed to do this with this expense.")]
    public async Task SubmitAsync_ServiceOutcome_BecomesTheStatusCodeAndTitle(ExpenseOperationStatus outcome, int expectedStatusCode, string expectedTitle)
    {
        var service = new FakeExpenseService { Result = ResultOf(outcome) };

        IActionResult result = await NewController(service, UserId, "Employee").SubmitAsync(Guid.NewGuid());

        ProblemDetails problem = ProblemOf(result);
        Assert.AreEqual(expectedStatusCode, problem.Status);
        Assert.AreEqual(expectedTitle, problem.Title);
        Assert.HasCount(1, service.Calls);
    }

    /// <summary>A wrong state is a 409 whose title belongs to the action and does not say the current state of the expense.</summary>
    /// <param name="endpoint">The action.</param>
    /// <param name="expectedTitle">The title of the conflict for that action.</param>
    [TestMethod]
    [DataRow("update", "The expense is not a draft.")]
    [DataRow("submit", "The expense is not a draft.")]
    [DataRow("approve", "The expense is not submitted.")]
    [DataRow("reject", "The expense is not submitted.")]
    [DataRow("pay", "The expense is not approved.")]
    public async Task Action_WrongState_Returns409WithTheTitleOfTheAction(string endpoint, string expectedTitle)
    {
        var service = new FakeExpenseService { Result = ExpenseOperationResult.WrongState() };

        IActionResult result = await Invoke(NewController(service, UserId, "Employee", "Approver", "Finance"), endpoint, Guid.NewGuid());

        ProblemDetails problem = ProblemOf(result);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
        Assert.AreEqual(expectedTitle, problem.Title);
    }

    /// <summary>An expense that does not exist and one outside the read scope get the very same 404 in the detail and in the history, and the same one an action that writes gives to a missing expense.</summary>
    [TestMethod]
    public async Task NotFound_InTheDetailTheHistoryAndTheActions_IsTheSameProblem()
    {
        var service = new FakeExpenseService { Result = ExpenseOperationResult.NotFound() };
        ExpensesController controller = NewController(service, UserId, "Employee", "Auditor");
        Guid id = Guid.NewGuid();

        ProblemDetails fromAction = ProblemOf(await controller.SubmitAsync(id));
        ProblemDetails fromDetail = ProblemOf((await controller.GetAsync(id)).Result!);
        ProblemDetails fromHistory = ProblemOf((await controller.HistoryAsync(id)).Result!);

        foreach (ProblemDetails problem in new[] { fromDetail, fromHistory })
        {
            Assert.AreEqual(StatusCodes.Status404NotFound, problem.Status);
            Assert.AreEqual(fromAction.Title, problem.Title);
        }
    }

    /// <summary>The list answers with the expenses of the service, in the order they came, as responses.</summary>
    [TestMethod]
    public async Task ListAsync_ReturnsTheVisibleExpensesInOrderAsResponses()
    {
        Expense first = ExpenseTestData.ExpenseOf("owner-a");
        Expense second = ExpenseTestData.ExpenseOf("owner-b");
        var service = new FakeExpenseService { ListResult = [second, first] };

        ActionResult<IReadOnlyList<ExpenseResponse>> result = await NewController(service, UserId, "Auditor").ListAsync();

        IReadOnlyList<ExpenseResponse> responses = result.Value!;
        CollectionAssert.AreEqual(new[] { second.Id, first.Id }, responses.Select(response => response.Id).ToArray());
        CollectionAssert.AreEqual(new[] { nameof(ExpenseService.ListAsync) }, service.Calls.ToArray());
        Assert.AreEqual(UserId, service.LastCaller!.UserId);
        Assert.IsTrue(service.LastCaller.IsInRole("Auditor"));
    }

    /// <summary>A readable expense is answered with its response and the identifier of the route is the one passed to the service.</summary>
    [TestMethod]
    public async Task GetAsync_ReadableExpense_ReturnsItsResponse()
    {
        Expense expense = ExpenseTestData.ExpenseOf(Owner);
        var service = new FakeExpenseService { GetResult = expense };

        ActionResult<ExpenseResponse> result = await NewController(service, UserId, "Employee").GetAsync(expense.Id);

        Assert.AreEqual(expense.Id, result.Value!.Id);
        Assert.AreEqual(expense.Id, service.LastExpenseId);
    }

    /// <summary>The history is answered in the order of the service, as responses.</summary>
    [TestMethod]
    public async Task HistoryAsync_ReturnsTheEntriesInOrderAsResponses()
    {
        Expense expense = ExpenseTestData.ExpenseOf(Owner);
        var entries = new List<ExpenseHistory>
        {
            new() { Id = 1, ExpenseId = expense.Id, Action = ExpenseHistoryAction.Created, ActorId = Owner, NewStatus = ExpenseStatus.Draft },
            new() { Id = 2, ExpenseId = expense.Id, Action = ExpenseHistoryAction.Submitted, ActorId = Owner, PreviousStatus = ExpenseStatus.Draft, NewStatus = ExpenseStatus.Submitted },
        };
        var service = new FakeExpenseService { HistoryResult = entries };

        ActionResult<IReadOnlyList<ExpenseHistoryResponse>> result = await NewController(service, UserId, "Employee").HistoryAsync(expense.Id);

        int[] expectedIds = [1, 2];
        CollectionAssert.AreEqual(expectedIds, result.Value!.Select(entry => entry.Id).ToArray());
        Assert.AreEqual(expense.Id, service.LastExpenseId);
    }

    /// <summary>The user comes only from the claims: without an identifier the controller fails before it calls the service.</summary>
    [TestMethod]
    public async Task Action_PrincipalWithoutUserIdentifier_ThrowsAndDoesNotCallTheService()
    {
        var service = new FakeExpenseService();
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.Role, "Employee"));
        ExpensesController controller = NewController(service, new ClaimsPrincipal(identity));

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SubmitAsync(Guid.NewGuid()));

        Assert.IsEmpty(service.Calls);
    }

    private static ExpensesController NewController(FakeExpenseService service, string userId, params string[] roles)
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));

        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return NewController(service, new ClaimsPrincipal(identity));
    }

    private static ExpensesController NewController(FakeExpenseService service, ClaimsPrincipal principal)
    {
        return new ExpensesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
            ProblemDetailsFactory = new FakeProblemDetailsFactory(),
        };
    }

    private static ExpenseRequest ValidRequest()
    {
        return new ExpenseRequest { Description = "Almoco com cliente em Campinas", Amount = 87.50m, ExpenseDate = new DateOnly(2026, 10, 1) };
    }

    private static Task<IActionResult> Invoke(ExpensesController controller, string endpoint, Guid id)
    {
        return endpoint switch
        {
            "update" => controller.UpdateAsync(id, ValidRequest()),
            "submit" => controller.SubmitAsync(id),
            "approve" => controller.ApproveAsync(id),
            "reject" => controller.RejectAsync(id, new RejectExpenseRequest { Reason = "Falta o comprovante da despesa" }),
            "pay" => controller.PayAsync(id),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown action."),
        };
    }

    private static ExpenseOperationResult ResultOf(ExpenseOperationStatus outcome)
    {
        return outcome switch
        {
            ExpenseOperationStatus.NotFound => ExpenseOperationResult.NotFound(),
            ExpenseOperationStatus.Forbidden => ExpenseOperationResult.Forbidden(),
            ExpenseOperationStatus.WrongState => ExpenseOperationResult.WrongState(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a failure."),
        };
    }

    private static ProblemDetails ProblemOf(IActionResult result)
    {
        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(result);

        return Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
    }
}
