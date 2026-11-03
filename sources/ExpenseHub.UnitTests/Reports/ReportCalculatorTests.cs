using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the calculation of a report: which expenses belong to it (the period on the date of the expense, both limits
/// inclusive, and the scope), the five states that are always present, the empty result, the sum in cents with a decimal
/// output, and the fact that the calculation never changes an expense. The calculator runs on a list in memory here, with the
/// same query rule that SQLite applies in production.
/// </summary>
[TestClass]
public sealed class ReportCalculatorTests
{
    private static readonly ExpenseStatus[] _allStatuses =
        [ExpenseStatus.Draft, ExpenseStatus.Submitted, ExpenseStatus.Approved, ExpenseStatus.Rejected, ExpenseStatus.Paid];

    /// <summary>The example of the assignment for Mine gives three expenses and 175.00: two Approved adding 150.00 and one Paid of 25.00.</summary>
    [TestMethod]
    public void Calculate_ExampleOfTheAssignmentForMine_GivesThreeExpensesAnd175()
    {
        ReportSummary summary = ReportTestData.Calculate(ReportTestData.ExampleExpenses(), ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(3, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("175.00"), summary.TotalAmount);
        AssertTotal(summary, ExpenseStatus.Approved, 2, "150.00");
        AssertTotal(summary, ExpenseStatus.Paid, 1, "25.00");
        AssertTotal(summary, ExpenseStatus.Draft, 0, "0.00");
        AssertTotal(summary, ExpenseStatus.Submitted, 0, "0.00");
        AssertTotal(summary, ExpenseStatus.Rejected, 0, "0.00");
    }

    /// <summary>The example of the assignment for Global gives four expenses and 375.00, still leaving out the one outside the period.</summary>
    [TestMethod]
    public void Calculate_ExampleOfTheAssignmentForGlobal_GivesFourExpensesAnd375()
    {
        ReportSummary summary = ReportTestData.Calculate(ReportTestData.ExampleExpenses(), ReportTestData.Query(ReportScope.Global));

        Assert.AreEqual(4, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("375.00"), summary.TotalAmount);
        AssertTotal(summary, ExpenseStatus.Approved, 3, "350.00");
        AssertTotal(summary, ExpenseStatus.Paid, 1, "25.00");
    }

    /// <summary>An expense on the first day and one on the last day of the period are both counted, because both limits are inclusive.</summary>
    [TestMethod]
    public void Calculate_ExpensesOnTheFirstAndOnTheLastDay_AreCounted()
    {
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Approved, "10.00", ReportTestData.From),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Approved, "20.00", ReportTestData.To),
        ];

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(2, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("30.00"), summary.TotalAmount);
    }

    /// <summary>An expense one day before the period and one a day after it are not counted.</summary>
    [TestMethod]
    public void Calculate_ExpensesTheDayBeforeAndTheDayAfterThePeriod_AreNotCounted()
    {
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Approved, "10.00", ReportTestData.From.AddDays(-1)),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Approved, "20.00", ReportTestData.To.AddDays(1)),
        ];

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(0, summary.TotalCount);
        Assert.AreEqual(0m, summary.TotalAmount);
    }

    /// <summary>A period of a single day counts the expenses of that day and no other.</summary>
    [TestMethod]
    public void Calculate_PeriodOfOneDay_CountsOnlyThatDay()
    {
        var day = new DateOnly(2026, 9, 10);
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "5.00", day),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "7.00", day.AddDays(1)),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "9.00", day.AddDays(-1)),
        ];

        ReportSummary summary = ReportTestData.Calculate(expenses, new ReportQuery(ReportScope.Mine, ReportTestData.Requester, day, day));

        Assert.AreEqual(1, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("5.00"), summary.TotalAmount);
    }

    /// <summary>The period is read on the date of the expense and never on the date it was created.</summary>
    [TestMethod]
    public void Calculate_FiltersTheDateOfTheExpenseAndNotTheCreationDate()
    {
        DateTimeOffset createdInside = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset createdOutside = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "40.00", new DateOnly(2026, 8, 15), createdInside),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "60.00", new DateOnly(2026, 9, 15), createdOutside),
        ];

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(1, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("60.00"), summary.TotalAmount);
    }

    /// <summary>With no expense at all, the five states are present with zero and so are the totals.</summary>
    [TestMethod]
    public void Calculate_NoExpenses_GivesFiveStatesAndTotalsAtZero()
    {
        ReportSummary summary = ReportTestData.Calculate([], ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(0, summary.TotalCount);
        Assert.AreEqual(0m, summary.TotalAmount);
        Assert.HasCount(5, summary.ByStatus);

        foreach (ReportStatusTotal total in summary.ByStatus)
        {
            Assert.AreEqual(0, total.Count, total.Status.ToString());
            Assert.AreEqual(0m, total.Amount, total.Status.ToString());
        }
    }

    /// <summary>When every expense is outside the period or the scope, the result is the empty one: five states at zero.</summary>
    [TestMethod]
    public void Calculate_NoEligibleExpense_GivesFiveStatesAndTotalsAtZero()
    {
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Other, ExpenseStatus.Approved, "200.00", new DateOnly(2026, 9, 10)),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "500.00", new DateOnly(2026, 10, 1)),
        ];

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(0, summary.TotalCount);
        Assert.AreEqual(0m, summary.TotalAmount);
        Assert.HasCount(5, summary.ByStatus);
        Assert.IsTrue(summary.ByStatus.All(total => total.Count == 0 && total.Amount == 0m));
    }

    /// <summary>The five states are always in the result, once each and in the order Draft, Submitted, Approved, Rejected, Paid, whatever the expenses are.</summary>
    [TestMethod]
    public void Calculate_ListsTheFiveStatesInOrderWhateverTheExpensesAre()
    {
        List<Expense> expenses = [ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "1.00", ReportTestData.From)];

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        CollectionAssert.AreEqual(_allStatuses, summary.ByStatus.Select(total => total.Status).ToArray());
    }

    /// <summary>Every state is counted on its own line: an expense in each of the five states adds one to each.</summary>
    [TestMethod]
    public void Calculate_OneExpenseInEachState_CountsOneInEachLine()
    {
        List<Expense> expenses = _allStatuses
            .Select((status, index) => ReportTestData.Expense(ReportTestData.Requester, status, $"{index + 1}.00", ReportTestData.From))
            .ToList();

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(5, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("15.00"), summary.TotalAmount);
        AssertTotal(summary, ExpenseStatus.Draft, 1, "1.00");
        AssertTotal(summary, ExpenseStatus.Submitted, 1, "2.00");
        AssertTotal(summary, ExpenseStatus.Approved, 1, "3.00");
        AssertTotal(summary, ExpenseStatus.Rejected, 1, "4.00");
        AssertTotal(summary, ExpenseStatus.Paid, 1, "5.00");
    }

    /// <summary>The scope Mine never adds an expense of another employee, even inside the period.</summary>
    [TestMethod]
    public void Calculate_Mine_NeverAddsTheExpensesOfAnotherOwner()
    {
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Other, ExpenseStatus.Approved, "999.00", new DateOnly(2026, 9, 5)),
            ReportTestData.Expense(ReportTestData.Other, ExpenseStatus.Paid, "1.00", new DateOnly(2026, 9, 6)),
        ];

        ReportSummary mine = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));
        ReportSummary global = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Global));

        Assert.AreEqual(0, mine.TotalCount);
        Assert.AreEqual(2, global.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("1000.00"), global.TotalAmount);
    }

    /// <summary>The scope Mine filters by the requester of the query: the same expenses give another result for another requester.</summary>
    [TestMethod]
    public void Calculate_Mine_UsesTheRequesterOfTheQuery()
    {
        ReportSummary requester = ReportTestData.Calculate(ReportTestData.ExampleExpenses(), ReportTestData.Query(ReportScope.Mine, ReportTestData.Requester));
        ReportSummary other = ReportTestData.Calculate(ReportTestData.ExampleExpenses(), ReportTestData.Query(ReportScope.Mine, ReportTestData.Other));

        Assert.AreEqual(3, requester.TotalCount);
        Assert.AreEqual(1, other.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("200.00"), other.TotalAmount);
    }

    /// <summary>The sum is exact in cents: three amounts of 0.10 give 0.30, which a floating point sum would not.</summary>
    [TestMethod]
    public void Calculate_AmountsThatBreakFloatingPoint_AreExact()
    {
        List<Expense> expenses = Enumerable.Range(0, 3)
            .Select(_ => ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Approved, "0.10", ReportTestData.From))
            .ToList();

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(ReportTestData.Dec("0.30"), summary.TotalAmount);
        Assert.AreEqual(ReportTestData.Dec("0.30"), summary.ByStatus.Single(total => total.Status == ExpenseStatus.Approved).Amount);
    }

    /// <summary>A thousand amounts of one cent add up to exactly ten, without any drift.</summary>
    [TestMethod]
    public void Calculate_ThousandOneCentAmounts_AddUpToExactlyTen()
    {
        List<Expense> expenses = Enumerable.Range(0, 1000)
            .Select(_ => ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "0.01", ReportTestData.From))
            .ToList();

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(1000, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("10.00"), summary.TotalAmount);
    }

    /// <summary>The largest amount an expense can have, plus one cent, is added exactly.</summary>
    [TestMethod]
    public void Calculate_LargestAmountPlusOneCent_IsExact()
    {
        List<Expense> expenses =
        [
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "2147483647.00", ReportTestData.From),
            ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Paid, "0.01", ReportTestData.From),
        ];

        ReportSummary summary = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        Assert.AreEqual(ReportTestData.Dec("2147483647.01"), summary.TotalAmount);
    }

    /// <summary>The instant of generation is the one given, and the total is the sum of the five states.</summary>
    [TestMethod]
    public void Calculate_RecordsTheInstantGivenAndTotalsTheStates()
    {
        ReportSummary summary = ReportTestData.MineSummary();

        Assert.AreEqual(ReportTestData.Now, summary.GeneratedAtUtc);
        Assert.AreEqual(summary.ByStatus.Sum(total => total.Count), summary.TotalCount);
        Assert.AreEqual(summary.ByStatus.Sum(total => total.Amount), summary.TotalAmount);
    }

    /// <summary>The order of the expenses does not change the result.</summary>
    [TestMethod]
    public void Calculate_DoesNotDependOnTheOrderOfTheExpenses()
    {
        List<Expense> expenses = ReportTestData.ExampleExpenses();
        List<Expense> reversed = ReportTestData.ExampleExpenses();
        reversed.Reverse();

        ReportSummary first = ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Global));
        ReportSummary second = ReportTestData.Calculate(reversed, ReportTestData.Query(ReportScope.Global));

        Assert.AreEqual(first.TotalCount, second.TotalCount);
        Assert.AreEqual(first.TotalAmount, second.TotalAmount);
        CollectionAssert.AreEqual(first.ByStatus.ToArray(), second.ByStatus.ToArray());
    }

    /// <summary>The calculation only reads: no expense changes its state, amount, date, owner or history.</summary>
    [TestMethod]
    public void Calculate_IsReadOnly_NoExpenseChanges()
    {
        List<Expense> expenses = ReportTestData.ExampleExpenses();
        string[] before = expenses.Select(Snapshot).ToArray();

        ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Global));
        ReportTestData.Calculate(expenses, ReportTestData.Query(ReportScope.Mine));

        CollectionAssert.AreEqual(before, expenses.Select(Snapshot).ToArray());
    }

    /// <summary>Summarizing the same lines twice gives the same result, so a repeated calculation has no effect of its own.</summary>
    [TestMethod]
    public void Summarize_SameLinesTwice_GiveTheSameResult()
    {
        ReportExpenseLine[] lines = [new(ExpenseStatus.Approved, 10.50m), new(ExpenseStatus.Paid, 4.25m)];

        ReportSummary first = ReportCalculator.Summarize(lines, ReportTestData.Now);
        ReportSummary second = ReportCalculator.Summarize(lines, ReportTestData.Now);

        Assert.AreEqual(first.TotalCount, second.TotalCount);
        Assert.AreEqual(first.TotalAmount, second.TotalAmount);
        Assert.AreEqual(ReportTestData.Dec("14.75"), first.TotalAmount);
    }

    /// <summary>A query needs a requester, so the scope Mine can never fall back to everyone.</summary>
    /// <param name="requesterId">A requester that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Query_BlankRequester_IsRejected(string? requesterId)
    {
        Assert.Throws<ArgumentException>(() => new ReportQuery(ReportScope.Mine, requesterId!, ReportTestData.From, ReportTestData.To));
        Assert.Throws<ArgumentException>(() => new ReportQuery(ReportScope.Global, requesterId!, ReportTestData.From, ReportTestData.To));
    }

    /// <summary>A query whose period ends before it starts is rejected.</summary>
    [TestMethod]
    public void Query_PeriodEndingBeforeItStarts_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ReportQuery(ReportScope.Mine, ReportTestData.Requester, ReportTestData.To, ReportTestData.From));
    }

    /// <summary>The calculator does not accept missing arguments.</summary>
    [TestMethod]
    public void Calculator_MissingArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportCalculator.Select(null!, ReportTestData.Query(ReportScope.Mine)));
        Assert.Throws<ArgumentNullException>(() => ReportCalculator.Select(Array.Empty<Expense>().AsQueryable(), null!));
        Assert.Throws<ArgumentNullException>(() => ReportCalculator.Summarize(null!, ReportTestData.Now));
    }

    private static void AssertTotal(ReportSummary summary, ExpenseStatus status, int count, string amount)
    {
        ReportStatusTotal total = summary.ByStatus.Single(candidate => candidate.Status == status);

        Assert.AreEqual(count, total.Count, $"{status} count");
        Assert.AreEqual(ReportTestData.Dec(amount), total.Amount, $"{status} amount");
    }

    private static string Snapshot(Expense expense)
    {
        return $"{expense.Id}|{expense.OwnerId}|{expense.Status}|{expense.Amount}|{expense.ExpenseDate:yyyy-MM-dd}|{expense.CreatedAtUtc:O}|{expense.History.Count}";
    }
}
