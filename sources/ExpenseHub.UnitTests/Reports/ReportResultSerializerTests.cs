using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of how the result of a report is saved and read back: money in integer cents, the instant of generation kept, and
/// no result that breaks the contract (five states once each, no negative value, totals equal to the sums) is ever written
/// or accepted when read. Reading never recalculates anything.
/// </summary>
[TestClass]
public sealed class ReportResultSerializerTests
{
    private const string Generated = "2026-10-02T15:00:00+00:00";

    // The part of the message of the check "each of the five states exactly once".
    private const string FiveStatesMessage = "five states";

    /// <summary>A result written and read back keeps the counts, the amounts, the totals and the instant of generation.</summary>
    [TestMethod]
    public void RoundTrip_KeepsCountsAmountsTotalsAndTheInstant()
    {
        ReportSummary original = ReportTestData.MineSummary();

        ReportSummary read = ReportResultSerializer.Deserialize(ReportResultSerializer.Serialize(original));

        Assert.AreEqual(original.GeneratedAtUtc, read.GeneratedAtUtc);
        Assert.AreEqual(original.TotalCount, read.TotalCount);
        Assert.AreEqual(original.TotalAmount, read.TotalAmount);
        CollectionAssert.AreEqual(original.ByStatus.ToArray(), read.ByStatus.ToArray());
    }

    /// <summary>The empty result (five states at zero) is saved and read back as it is.</summary>
    [TestMethod]
    public void RoundTrip_EmptyResult_KeepsTheFiveStatesAtZero()
    {
        ReportSummary empty = ReportTestData.Calculate([], ReportTestData.Query(ReportScope.Mine));

        ReportSummary read = ReportResultSerializer.Deserialize(ReportResultSerializer.Serialize(empty));

        Assert.AreEqual(0, read.TotalCount);
        Assert.AreEqual(0m, read.TotalAmount);
        Assert.HasCount(5, read.ByStatus);
    }

    /// <summary>The saved JSON holds the money as integer cents and the states by name, never decimals.</summary>
    [TestMethod]
    public void Serialize_SavesMoneyAsIntegerCentsAndStatesByName()
    {
        string json = ReportResultSerializer.Serialize(ReportTestData.MineSummary());

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.AreEqual(17500L, root.GetProperty("totalAmountCents").GetInt64());
        Assert.AreEqual(3, root.GetProperty("totalCount").GetInt32());
        JsonElement approved = root.GetProperty("byStatus").EnumerateArray().Single(line => line.GetProperty("status").GetString() == "Approved");
        Assert.AreEqual(15000L, approved.GetProperty("amountCents").GetInt64());
        Assert.AreEqual(2, approved.GetProperty("count").GetInt32());
        Assert.DoesNotContain(".", root.GetProperty("totalAmountCents").GetRawText());
    }

    /// <summary>An instant with another offset is saved as the same instant in UTC.</summary>
    [TestMethod]
    public void Serialize_InstantWithOffset_IsSavedInUtc()
    {
        ReportSummary summary = ReportTestData.MineSummary() with { GeneratedAtUtc = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-3)) };

        ReportSummary read = ReportResultSerializer.Deserialize(ReportResultSerializer.Serialize(summary));

        Assert.AreEqual(new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero), read.GeneratedAtUtc);
        Assert.AreEqual(TimeSpan.Zero, read.GeneratedAtUtc.Offset);
    }

    /// <summary>A result whose total is not the sum of its states is never written.</summary>
    [TestMethod]
    public void Serialize_TotalThatIsNotTheSumOfTheStates_Throws()
    {
        ReportSummary summary = ReportTestData.MineSummary() with { TotalCount = 99 };

        Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Serialize(summary));
    }

    /// <summary>A result with a state missing or repeated is never written.</summary>
    [TestMethod]
    public void Serialize_StateMissingOrRepeated_Throws()
    {
        ReportSummary good = ReportTestData.MineSummary();
        ReportSummary missing = good with { ByStatus = good.ByStatus.Take(4).ToList() };
        ReportSummary repeated = good with { ByStatus = good.ByStatus.Take(4).Append(good.ByStatus[0]).ToList() };

        // The cause is checked, not only the type: another check further on would also throw the same type by accident.
        InvalidOperationException forMissing = Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Serialize(missing));
        InvalidOperationException forRepeated = Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Serialize(repeated));
        Assert.Contains(FiveStatesMessage, forMissing.Message);
        Assert.Contains(FiveStatesMessage, forRepeated.Message);
    }

    /// <summary>Reading a saved result with a state missing is an error, not a guess.</summary>
    [TestMethod]
    public void Deserialize_StateMissing_Throws()
    {
        string json = Json(["Draft", "Submitted", "Approved", "Rejected"], 0, 0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
        Assert.Contains(FiveStatesMessage, error.Message);
    }

    /// <summary>Reading a saved result with a state repeated is an error.</summary>
    [TestMethod]
    public void Deserialize_StateRepeated_Throws()
    {
        string json = Json(["Draft", "Draft", "Approved", "Rejected", "Paid"], 0, 0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
        Assert.Contains(FiveStatesMessage, error.Message);
    }

    /// <summary>Reading a saved result with an unknown state is an error.</summary>
    [TestMethod]
    public void Deserialize_UnknownState_Throws()
    {
        string json = Json(["Draft", "Submitted", "Approved", "Rejected", "Archived"], 0, 0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
        Assert.Contains("cannot be read", error.Message);
    }

    /// <summary>Reading a saved result that gives a state as a number is an error: the states are saved by name.</summary>
    [TestMethod]
    public void Deserialize_StateAsNumber_Throws()
    {
        string json = Json(["0", "Submitted", "Approved", "Rejected", "Paid"], 0, 0).Replace("\"0\"", "0", StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
    }

    /// <summary>Reading a saved result whose total count is not the sum of the states is an error.</summary>
    [TestMethod]
    public void Deserialize_TotalCountThatIsNotTheSum_Throws()
    {
        string json = Json(["Draft", "Submitted", "Approved", "Rejected", "Paid"], 7, 0);

        Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
    }

    /// <summary>Reading a saved result with a negative amount is an error.</summary>
    [TestMethod]
    public void Deserialize_NegativeAmount_Throws()
    {
        string json = Json(["Draft", "Submitted", "Approved", "Rejected", "Paid"], 0, -100);

        Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
    }

    /// <summary>Text that is not JSON, or the JSON value null, is an error.</summary>
    /// <param name="json">The text.</param>
    [TestMethod]
    [DataRow("not json")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{\"totalCount\": \"many\"}")]
    public void Deserialize_TextThatIsNotAResult_Throws(string json)
    {
        Assert.Throws<InvalidOperationException>(() => ReportResultSerializer.Deserialize(json));
    }

    /// <summary>An empty text is rejected before it is read.</summary>
    /// <param name="json">A text that is missing or blank.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Deserialize_BlankText_IsRejected(string? json)
    {
        Assert.Throws<ArgumentException>(() => ReportResultSerializer.Deserialize(json!));
    }

    /// <summary>Reading returns the states in the fixed order, even when the saved text lists them in another one.</summary>
    [TestMethod]
    public void Deserialize_StatesInAnotherOrder_AreReturnedInTheFixedOrder()
    {
        string json = Json(["Paid", "Rejected", "Approved", "Submitted", "Draft"], 0, 0);

        ReportSummary read = ReportResultSerializer.Deserialize(json);

        CollectionAssert.AreEqual(
            new[] { ExpenseStatus.Draft, ExpenseStatus.Submitted, ExpenseStatus.Approved, ExpenseStatus.Rejected, ExpenseStatus.Paid },
            read.ByStatus.Select(total => total.Status).ToArray());
    }

    private static string Json(IReadOnlyList<string> statuses, int totalCount, long totalAmountCents)
    {
        string lines = string.Join(",", statuses.Select(status => $"{{\"status\":{Quote(status)},\"count\":0,\"amountCents\":0}}"));

        return $"{{\"generatedAtUtc\":\"{Generated}\",\"totalCount\":{totalCount},\"totalAmountCents\":{totalAmountCents},\"byStatus\":[{lines}]}}";
    }

    private static string Quote(string status)
    {
        return $"\"{status}\"";
    }
}
