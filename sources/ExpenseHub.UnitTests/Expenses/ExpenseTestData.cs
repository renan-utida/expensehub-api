using System;
using System.Globalization;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Shared data of the expense tests. The fixed instant is 12:00 in Brasilia time on 2026-10-02.
/// </summary>
internal static class ExpenseTestData
{
    /// <summary>Gets the instant the tests run at (2026-10-02 15:00 UTC, which is 12:00 in Brasilia).</summary>
    public static DateTimeOffset Now { get; } = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Gets today's date in Brasilia time for <see cref="Now"/>.</summary>
    public static DateOnly Today { get; } = new DateOnly(2026, 10, 2);

    /// <summary>Gets valid expense fields.</summary>
    public static ExpenseDetails Valid { get; } = new ExpenseDetails("Almoco com cliente em Campinas", 87.50m, new DateOnly(2026, 10, 1));

    /// <summary>Parses a decimal written with a dot, whatever the culture of the machine is.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    public static decimal Dec(string text)
    {
        return decimal.Parse(text, CultureInfo.InvariantCulture);
    }

    /// <summary>Builds an authenticated user with the given roles.</summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roles">The roles of the user.</param>
    /// <returns>The user.</returns>
    public static ExpenseCaller Caller(string userId, params string[] roles)
    {
        return new ExpenseCaller(userId, roles);
    }

    /// <summary>Builds an expense with a label in its description, owned by a user, in the given state.</summary>
    /// <param name="label">The label, kept in the description so tests can tell expenses apart.</param>
    /// <param name="ownerId">The owner.</param>
    /// <param name="status">The state.</param>
    /// <returns>The expense.</returns>
    public static Expense Labeled(string label, string ownerId, ExpenseStatus status)
    {
        Expense expense = ExpenseOf(ownerId, status);
        expense.Description = label;

        return expense;
    }

    /// <summary>Builds an expense that belongs to a user, in the given state.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="status">The state.</param>
    /// <returns>The expense, with a single creation history entry.</returns>
    public static Expense ExpenseOf(string ownerId, ExpenseStatus status = ExpenseStatus.Draft)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Description = "Taxi para o aeroporto",
            Amount = 60.00m,
            ExpenseDate = new DateOnly(2026, 9, 20),
            Status = status,
            CreatedAtUtc = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero),
        };

        expense.History.Add(new ExpenseHistory
        {
            Action = ExpenseHistoryAction.Created,
            ActorId = ownerId,
            OccurredAtUtc = expense.CreatedAtUtc,
            PreviousStatus = null,
            NewStatus = ExpenseStatus.Draft,
        });

        return expense;
    }
}
