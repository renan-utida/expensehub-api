using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Rules of creating, editing and submitting expense drafts, and of reading expenses by profile.
/// The owner, the state, the actor and the instants always come from the caller (the token)
/// and the server clock, never from the client data. Every decision that depends on the role, the owner and the state
/// of an expense is made here through <see cref="ExpenseAccess"/>, and not in the controller.
/// </summary>
public sealed class ExpenseService
{
    private const int ShortTextLength = 80;

    private readonly IExpenseRepository _repository;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpenseService"/> class.
    /// </summary>
    /// <param name="repository">The expense storage.</param>
    /// <param name="timeProvider">The clock of the server.</param>
    public ExpenseService(IExpenseRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a draft owned by the caller and records its first history entry in the same save.
    /// Only a user with the <c>Employee</c> role can create. The answers follow this order: no role, then invalid data.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="details">The fields chosen by the client.</param>
    /// <returns>The created draft, or the reason it was not created.</returns>
    public async Task<ExpenseOperationResult> CreateAsync(ExpenseCaller caller, ExpenseDetails details)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);
        ArgumentNullException.ThrowIfNull(details);

        if (ExpenseAccess.Evaluate(caller, ExpenseAction.Create, null) != AccessDecision.Allowed)
        {
            return ExpenseOperationResult.Forbidden();
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<ExpenseValidationError> errors = Validate(details, now);

        if (errors.Count > 0)
        {
            return ExpenseOperationResult.Invalid(errors);
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = caller.UserId,
            Description = details.Description!.Trim(),
            Amount = details.Amount!.Value,
            ExpenseDate = details.ExpenseDate!.Value,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = now,
        };

        expense.History.Add(new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Created,
            ActorId = caller.UserId,
            OccurredAtUtc = now,
            PreviousStatus = null,
            NewStatus = ExpenseStatus.Draft,
        });

        await _repository.AddAsync(expense);

        return ExpenseOperationResult.Success(expense);
    }

    /// <summary>
    /// Replaces the description, the amount and the date of a draft, and records the history entry in the same save.
    /// The owner, the state and the creation instant never change.
    /// The answers follow this order: no role (<c>403</c>), invalid data (<c>400</c>), the expense does not exist (<c>404</c>),
    /// the expense belongs to someone else (<c>403</c>) and not a draft (<c>409</c>). The read scope of the caller is not used.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="expenseId">The identifier of the expense.</param>
    /// <param name="details">The fields chosen by the client.</param>
    /// <returns>The edited draft, or the reason it was not edited.</returns>
    public async Task<ExpenseOperationResult> UpdateAsync(ExpenseCaller caller, Guid expenseId, ExpenseDetails details)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);
        ArgumentNullException.ThrowIfNull(details);

        if (!ExpenseAccess.HasRoleFor(caller, ExpenseAction.Edit))
        {
            return ExpenseOperationResult.Forbidden();
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<ExpenseValidationError> errors = Validate(details, now);

        if (errors.Count > 0)
        {
            return ExpenseOperationResult.Invalid(errors);
        }

        Expense? expense = await _repository.FindByIdAsync(expenseId);

        switch (ExpenseAccess.Evaluate(caller, ExpenseAction.Edit, expense))
        {
            case AccessDecision.NotFound:
                return ExpenseOperationResult.NotFound();
            case AccessDecision.Forbidden:
                return ExpenseOperationResult.Forbidden();
            case AccessDecision.WrongState:
                return ExpenseOperationResult.NotDraft();
        }

        string description = details.Description!.Trim();
        decimal amount = details.Amount!.Value;
        DateOnly expenseDate = details.ExpenseDate!.Value;

        string changes = DescribeChanges(expense!, description, amount, expenseDate);

        expense!.Description = description;
        expense.Amount = amount;
        expense.ExpenseDate = expenseDate;

        expense.History.Add(new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Edited,
            ActorId = caller.UserId,
            OccurredAtUtc = now,
            PreviousStatus = ExpenseStatus.Draft,
            NewStatus = ExpenseStatus.Draft,
            Changes = changes,
        });

        try
        {
            await _repository.SaveChangesAsync();
        }
        catch (ExpenseConflictException)
        {
            // The state changed between the read and the save (for example, a concurrent submit).
            return ExpenseOperationResult.NotDraft();
        }

        return ExpenseOperationResult.Success(expense);
    }

    /// <summary>
    /// Submits a draft: <c>Draft</c> to <c>Submitted</c>, recording the history entry in the same save.
    /// The answers follow this order: no role (<c>403</c>), the expense does not exist (<c>404</c>),
    /// the expense belongs to someone else (<c>403</c>) and not a draft (<c>409</c>, which includes submitting twice).
    /// The read scope of the caller is not used.
    /// A concurrent submit of the same draft is also answered as not a draft, and writes no extra history.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="expenseId">The identifier of the expense.</param>
    /// <returns>The submitted expense, or the reason it was not submitted.</returns>
    public async Task<ExpenseOperationResult> SubmitAsync(ExpenseCaller caller, Guid expenseId)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.UserId);

        if (!ExpenseAccess.HasRoleFor(caller, ExpenseAction.Submit))
        {
            return ExpenseOperationResult.Forbidden();
        }

        Expense? expense = await _repository.FindByIdAsync(expenseId);

        switch (ExpenseAccess.Evaluate(caller, ExpenseAction.Submit, expense))
        {
            case AccessDecision.NotFound:
                return ExpenseOperationResult.NotFound();
            case AccessDecision.Forbidden:
                return ExpenseOperationResult.Forbidden();
            case AccessDecision.WrongState:
                return ExpenseOperationResult.NotDraft();
        }

        expense!.Status = ExpenseStatus.Submitted;
        expense.History.Add(new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Submitted,
            ActorId = caller.UserId,
            OccurredAtUtc = _timeProvider.GetUtcNow(),
            PreviousStatus = ExpenseStatus.Draft,
            NewStatus = ExpenseStatus.Submitted,
        });

        try
        {
            await _repository.SaveChangesAsync();
        }
        catch (ExpenseConflictException)
        {
            return ExpenseOperationResult.NotDraft();
        }

        return ExpenseOperationResult.Success(expense);
    }

    /// <summary>
    /// Lists the expenses the caller can read: the filter of the profile is applied by the storage inside the query.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <returns>The visible expenses, newest first; empty when none of the roles of the caller reads expenses.</returns>
    public async Task<IReadOnlyList<Expense>> ListAsync(ExpenseCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        ExpenseScope scope = ExpenseVisibility.ScopeFor(caller);

        return scope.IsEmpty ? Array.Empty<Expense>() : await _repository.ListAsync(scope);
    }

    /// <summary>
    /// Gets an expense the caller can read. An expense that does not exist and one outside the read scope
    /// of the caller are indistinguishable: both give <c>null</c>.
    /// </summary>
    /// <param name="caller">The authenticated user, taken from the token.</param>
    /// <param name="expenseId">The identifier of the expense.</param>
    /// <returns>The expense, or <c>null</c> when it does not exist or is not visible to the caller.</returns>
    public Task<Expense?> GetAsync(ExpenseCaller caller, Guid expenseId)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return FindVisibleAsync(caller, expenseId);
    }

    private async Task<Expense?> FindVisibleAsync(ExpenseCaller caller, Guid expenseId)
    {
        ExpenseScope scope = ExpenseVisibility.ScopeFor(caller);

        return scope.IsEmpty ? null : await _repository.FindVisibleAsync(expenseId, scope);
    }

    private static IReadOnlyList<ExpenseValidationError> Validate(ExpenseDetails details, DateTimeOffset now)
    {
        return ExpenseRules.Validate(details.Description, details.Amount, details.ExpenseDate, BrazilTime.Today(now));
    }

    private static string DescribeChanges(Expense expense, string description, decimal amount, DateOnly expenseDate)
    {
        var changes = new List<string>();

        if (!string.Equals(expense.Description, description, StringComparison.Ordinal))
        {
            changes.Add($"Description: \"{Shorten(expense.Description)}\" -> \"{Shorten(description)}\"");
        }

        if (expense.Amount != amount)
        {
            changes.Add($"Amount: {FormatAmount(expense.Amount)} -> {FormatAmount(amount)}");
        }

        if (expense.ExpenseDate != expenseDate)
        {
            changes.Add($"ExpenseDate: {FormatDate(expense.ExpenseDate)} -> {FormatDate(expenseDate)}");
        }

        string summary = changes.Count == 0 ? "No field changed." : string.Join("; ", changes);

        return summary.Length <= ExpenseRules.ChangesMaxLength ? summary : summary[..ExpenseRules.ChangesMaxLength];
    }

    private static string Shorten(string text)
    {
        return text.Length <= ShortTextLength ? text : text[..ShortTextLength] + "...";
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
