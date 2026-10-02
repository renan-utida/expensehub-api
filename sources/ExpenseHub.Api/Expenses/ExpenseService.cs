using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Rules of creating and editing expense drafts. The owner, the state, the actor and the instants
/// always come from the caller (the token) and the server clock, never from the client data.
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
    /// </summary>
    /// <param name="ownerId">The identifier of the authenticated user, taken from the token.</param>
    /// <param name="details">The fields chosen by the client.</param>
    /// <returns>The created draft, or the validation problems.</returns>
    public async Task<ExpenseOperationResult> CreateAsync(string ownerId, ExpenseDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(details);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<ExpenseValidationError> errors = Validate(details, now);

        if (errors.Count > 0)
        {
            return ExpenseOperationResult.Invalid(errors);
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Description = details.Description!.Trim(),
            Amount = details.Amount!.Value,
            ExpenseDate = details.ExpenseDate!.Value,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = now,
        };

        expense.History.Add(new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Created,
            ActorId = ownerId,
            OccurredAtUtc = now,
            PreviousStatus = null,
            NewStatus = ExpenseStatus.Draft,
        });

        await _repository.AddAsync(expense);

        return ExpenseOperationResult.Success(expense);
    }

    /// <summary>
    /// Replaces the description, the amount and the date of a draft that belongs to the caller, and records the
    /// history entry in the same save. The owner, the state and the creation instant never change.
    /// The order of the answers is: invalid data, then not found (also for someone else's expense), then not a draft.
    /// </summary>
    /// <param name="actorId">The identifier of the authenticated user, taken from the token.</param>
    /// <param name="expenseId">The identifier of the expense.</param>
    /// <param name="details">The fields chosen by the client.</param>
    /// <returns>The edited draft, or the reason it was not edited.</returns>
    public async Task<ExpenseOperationResult> UpdateAsync(string actorId, Guid expenseId, ExpenseDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(details);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<ExpenseValidationError> errors = Validate(details, now);

        if (errors.Count > 0)
        {
            return ExpenseOperationResult.Invalid(errors);
        }

        Expense? expense = await _repository.FindOwnedAsync(expenseId, actorId);

        if (expense is null)
        {
            return ExpenseOperationResult.NotFound();
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            return ExpenseOperationResult.NotDraft();
        }

        string description = details.Description!.Trim();
        decimal amount = details.Amount!.Value;
        DateOnly expenseDate = details.ExpenseDate!.Value;

        string changes = DescribeChanges(expense, description, amount, expenseDate);

        expense.Description = description;
        expense.Amount = amount;
        expense.ExpenseDate = expenseDate;

        expense.History.Add(new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Edited,
            ActorId = actorId,
            OccurredAtUtc = now,
            PreviousStatus = ExpenseStatus.Draft,
            NewStatus = ExpenseStatus.Draft,
            Changes = changes,
        });

        await _repository.SaveChangesAsync();

        return ExpenseOperationResult.Success(expense);
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
