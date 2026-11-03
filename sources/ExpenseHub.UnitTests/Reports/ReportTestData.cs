using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Shared data of the report tests. The period is September 2026 and the requester is <see cref="Requester"/>.
/// </summary>
internal static class ReportTestData
{
    /// <summary>The identifier of the user who requests the reports in the tests.</summary>
    public const string Requester = "requester-1";

    /// <summary>The identifier of another user.</summary>
    public const string Other = "other-2";

    /// <summary>Gets the first day of the period.</summary>
    public static DateOnly From { get; } = new DateOnly(2026, 9, 1);

    /// <summary>Gets the last day of the period.</summary>
    public static DateOnly To { get; } = new DateOnly(2026, 9, 30);

    /// <summary>Gets the instant the tests run at.</summary>
    public static DateTimeOffset Now { get; } = ExpenseTestData.Now;

    /// <summary>Parses a decimal written with a dot, whatever the culture of the machine is.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    public static decimal Dec(string text)
    {
        return decimal.Parse(text, CultureInfo.InvariantCulture);
    }

    /// <summary>Builds an authenticated user.</summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roles">The roles of the user.</param>
    /// <returns>The user.</returns>
    public static ExpenseCaller Caller(string userId, params string[] roles)
    {
        return ExpenseTestData.Caller(userId, roles);
    }

    /// <summary>Builds a query for the period of the tests.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="requesterId">The requester.</param>
    /// <returns>The query.</returns>
    public static ReportQuery Query(ReportScope scope, string requesterId = Requester)
    {
        return new ReportQuery(scope, requesterId, From, To);
    }

    /// <summary>Builds valid fields of a report request.</summary>
    /// <param name="scope">The name of the scope.</param>
    /// <returns>The fields.</returns>
    public static ReportRequestDetails Details(string? scope = "Mine")
    {
        return new ReportRequestDetails(From, To, scope);
    }

    /// <summary>Builds an expense with a date, an owner, a state and an amount.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="status">The state.</param>
    /// <param name="amount">The amount, written with a dot.</param>
    /// <param name="expenseDate">The date the expense happened.</param>
    /// <param name="createdAtUtc">The instant the expense was created.</param>
    /// <returns>The expense.</returns>
    public static Expense Expense(string ownerId, ExpenseStatus status, string amount, DateOnly expenseDate, DateTimeOffset? createdAtUtc = null)
    {
        Expense expense = ExpenseTestData.ExpenseOf(ownerId, status);
        expense.Amount = Dec(amount);
        expense.ExpenseDate = expenseDate;
        expense.CreatedAtUtc = createdAtUtc ?? new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

        return expense;
    }

    /// <summary>
    /// Gets the five expenses of the example of the assignment: three of the requester inside the period (Approved 100.00,
    /// Approved 50.00 and Paid 25.00), one of another user inside the period (Approved 200.00) and one of the requester
    /// outside the period (Paid 500.00). Two of the three inside expenses sit exactly on the first and the last day.
    /// </summary>
    /// <returns>The expenses.</returns>
    public static List<Expense> ExampleExpenses()
    {
        return
        [
            Expense(Requester, ExpenseStatus.Approved, "100.00", new DateOnly(2026, 9, 1)),
            Expense(Requester, ExpenseStatus.Approved, "50.00", new DateOnly(2026, 9, 15)),
            Expense(Requester, ExpenseStatus.Paid, "25.00", new DateOnly(2026, 9, 30)),
            Expense(Other, ExpenseStatus.Approved, "200.00", new DateOnly(2026, 9, 10)),
            Expense(Requester, ExpenseStatus.Paid, "500.00", new DateOnly(2026, 10, 1)),
        ];
    }

    /// <summary>Calculates the summary of a list of expenses, the way the worker does: select, then add up.</summary>
    /// <param name="expenses">The expenses.</param>
    /// <param name="query">The query.</param>
    /// <returns>The summary, generated at <see cref="Now"/>.</returns>
    public static ReportSummary Calculate(IEnumerable<Expense> expenses, ReportQuery query)
    {
        var lines = new List<ReportExpenseLine>(ReportCalculator.Select(new List<Expense>(expenses).AsQueryable(), query));

        return ReportCalculator.Summarize(lines, Now);
    }

    /// <summary>Gets the result of the example for the scope Mine: three expenses and 175.00.</summary>
    /// <returns>The summary.</returns>
    public static ReportSummary MineSummary()
    {
        return Calculate(ExampleExpenses(), Query(ReportScope.Mine));
    }

    /// <summary>Builds a pending report request of a user.</summary>
    /// <param name="requesterId">The requester.</param>
    /// <param name="status">The state.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The request, without a result.</returns>
    public static ReportRequest Report(string requesterId, ReportStatus status = ReportStatus.Pending, ReportScope scope = ReportScope.Mine)
    {
        return new ReportRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            Scope = scope,
            FromDate = From,
            ToDate = To,
            Status = status,
            CreatedAtUtc = Now,
        };
    }

    /// <summary>Builds a completed report request of a user, with its result saved.</summary>
    /// <param name="requesterId">The requester.</param>
    /// <param name="summary">The result to save, or the result of the example for Mine.</param>
    /// <returns>The completed request.</returns>
    public static ReportRequest CompletedReport(string requesterId, ReportSummary? summary = null)
    {
        ReportRequest report = Report(requesterId, ReportStatus.Completed);
        report.StartedAtUtc = Now.AddSeconds(1);
        report.CompletedAtUtc = Now.AddSeconds(2);
        report.ResultJson = ReportResultSerializer.Serialize(summary ?? MineSummary());

        return report;
    }

    /// <summary>Builds a failed report request of a user.</summary>
    /// <param name="requesterId">The requester.</param>
    /// <returns>The failed request, with a code and a message.</returns>
    public static ReportRequest FailedReport(string requesterId)
    {
        ReportRequest report = Report(requesterId, ReportStatus.Failed);
        report.StartedAtUtc = Now.AddSeconds(1);
        report.CompletedAtUtc = Now.AddSeconds(2);
        report.ErrorCode = "InvalidRequest";
        report.ErrorMessage = "The report could not be processed.";

        return report;
    }
}
