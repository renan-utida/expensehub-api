using System;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the date in Brasilia time used by the "not in the future" rule.
/// </summary>
[TestClass]
public sealed class BrazilTimeTests
{
    /// <summary>Before 03:00 UTC it is still the day before in Brasilia (UTC-3).</summary>
    [TestMethod]
    public void Today_BeforeThreeInTheMorningUtc_IsTheDayBefore()
    {
        var instant = new DateTimeOffset(2026, 10, 2, 2, 30, 0, TimeSpan.Zero);

        Assert.AreEqual(new DateOnly(2026, 10, 1), BrazilTime.Today(instant));
    }

    /// <summary>From 03:00 UTC on, the date in Brasilia is the same as the UTC date.</summary>
    [TestMethod]
    public void Today_AtThreeInTheMorningUtc_IsTheSameDay()
    {
        var instant = new DateTimeOffset(2026, 10, 2, 3, 0, 0, TimeSpan.Zero);

        Assert.AreEqual(new DateOnly(2026, 10, 2), BrazilTime.Today(instant));
    }

    /// <summary>In the middle of the day the date is the same in UTC and in Brasilia.</summary>
    [TestMethod]
    public void Today_InTheMiddleOfTheDay_IsTheSameDay()
    {
        Assert.AreEqual(ExpenseTestData.Today, BrazilTime.Today(ExpenseTestData.Now));
    }

    /// <summary>An instant given with another offset is converted by the real instant, not by its written offset.</summary>
    [TestMethod]
    public void Today_InstantWithAnotherOffset_UsesTheRealInstant()
    {
        var instant = new DateTimeOffset(2026, 10, 2, 0, 30, 0, TimeSpan.FromHours(3));

        Assert.AreEqual(new DateOnly(2026, 10, 1), BrazilTime.Today(instant));
    }
}
