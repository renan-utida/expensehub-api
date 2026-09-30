using System;
using ExpenseHub.Api.Persistence.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Persistence;

/// <summary>
/// Tests of the pure conversion between instants and UTC ticks.
/// </summary>
[TestClass]
public sealed class UtcTicksTests
{
    /// <summary>An instant with a non-zero offset is stored as the ticks of the same instant in UTC.</summary>
    [TestMethod]
    public void From_InstantWithOffset_UsesUtcTicks()
    {
        var instant = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(3));
        long expected = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).Ticks;

        Assert.AreEqual(expected, UtcTicks.From(instant));
    }

    /// <summary>Reading always returns an instant in UTC, with offset zero.</summary>
    [TestMethod]
    public void ToDateTimeOffset_Ticks_ReturnsOffsetZero()
    {
        DateTimeOffset instant = UtcTicks.ToDateTimeOffset(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).Ticks);

        Assert.AreEqual(TimeSpan.Zero, instant.Offset);
    }

    /// <summary>Converting and reading back keeps the same instant, normalized to UTC.</summary>
    [TestMethod]
    public void RoundTrip_InstantWithOffset_KeepsSameInstantInUtc()
    {
        var instant = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(3));

        DateTimeOffset restored = UtcTicks.ToDateTimeOffset(UtcTicks.From(instant));

        Assert.AreEqual(instant, restored);
        Assert.AreEqual(TimeSpan.Zero, restored.Offset);
    }

    /// <summary>Ticks follow the real chronological order even when the offsets differ, so ordering works in SQL.</summary>
    [TestMethod]
    public void From_EarlierInstantWithLaterClockTime_ProducesSmallerTicks()
    {
        var earlier = new DateTimeOffset(2026, 10, 1, 23, 0, 0, TimeSpan.FromHours(3));
        var later = new DateTimeOffset(2026, 10, 1, 21, 0, 0, TimeSpan.Zero);

        Assert.IsLessThan(UtcTicks.From(later), UtcTicks.From(earlier));
    }
}
