using System;
using ExpenseHub.Api.Persistence.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Persistence;

/// <summary>
/// Tests of the pure conversion between decimal amounts and integer cents.
/// </summary>
[TestClass]
public sealed class MoneyConversionTests
{
    /// <summary>The minimum allowed amount is stored as one cent.</summary>
    [TestMethod]
    public void ToCents_MinimumAmountOfOneCent_IsStoredAsOne()
    {
        Assert.AreEqual(1L, MoneyConversion.ToCents(0.01m));
    }

    /// <summary>Two decimal places become the integer cents without loss.</summary>
    [TestMethod]
    public void ToCents_AmountWithTwoDecimalPlaces_KeepsEveryCent()
    {
        Assert.AreEqual(123456L, MoneyConversion.ToCents(1234.56m));
    }

    /// <summary>The maximum allowed amount fits in the stored integer.</summary>
    [TestMethod]
    public void ToCents_MaximumAmountOfInt32MaxValue_FitsInLong()
    {
        Assert.AreEqual(214748364700L, MoneyConversion.ToCents(2147483647.00m));
    }

    /// <summary>Half a cent rounds away from zero.</summary>
    [TestMethod]
    public void ToCents_HalfCent_RoundsAwayFromZero()
    {
        Assert.AreEqual(1L, MoneyConversion.ToCents(0.005m));
        Assert.AreEqual(-1L, MoneyConversion.ToCents(-0.005m));
    }

    /// <summary>Less than half a cent rounds down.</summary>
    [TestMethod]
    public void ToCents_LessThanHalfCent_RoundsDown()
    {
        Assert.AreEqual(1000L, MoneyConversion.ToCents(10.004m));
    }

    /// <summary>An amount beyond the range of the stored integer is rejected instead of truncated.</summary>
    [TestMethod]
    public void ToCents_AmountBeyondLongRange_ThrowsOverflow()
    {
        Assert.ThrowsExactly<OverflowException>(() => MoneyConversion.ToCents(decimal.MaxValue));
    }

    /// <summary>Integer cents become the decimal amount.</summary>
    [TestMethod]
    public void FromCents_IntegerCents_RestoresDecimalAmount()
    {
        Assert.AreEqual(10.50m, MoneyConversion.FromCents(1050L));
    }

    /// <summary>Converting to cents and back keeps the amount.</summary>
    [TestMethod]
    public void RoundTrip_AmountWithTwoDecimalPlaces_IsPreserved()
    {
        Assert.AreEqual(1234.56m, MoneyConversion.FromCents(MoneyConversion.ToCents(1234.56m)));
    }

    /// <summary>A larger amount always becomes larger cents, so ordering and comparison work in SQL.</summary>
    [TestMethod]
    public void ToCents_LargerAmount_ProducesLargerCents()
    {
        Assert.IsLessThan(MoneyConversion.ToCents(9.99m), MoneyConversion.ToCents(0.01m));
        Assert.IsLessThan(MoneyConversion.ToCents(10.00m), MoneyConversion.ToCents(9.99m));
    }
}
