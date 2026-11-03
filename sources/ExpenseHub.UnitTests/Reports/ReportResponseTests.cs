using System;
using System.Linq;
using System.Text.Json;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the envelope of a report: each of the four states fills only its own fields, the instants are UTC and are written
/// ending in Z, the result has the five states, and the JSON has exactly the names of the contract.
/// </summary>
[TestClass]
public sealed class ReportResponseTests
{
    private static readonly JsonSerializerOptions _mvcOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A pending report has the creation instant and nothing else: no start, no completion, no result and no error.</summary>
    [TestMethod]
    public void From_PendingReport_HasOnlyTheCreationInstant()
    {
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester);

        ReportResponse envelope = ReportResponse.From(report);

        Assert.AreEqual(report.Id, envelope.Id);
        Assert.AreEqual("Mine", envelope.Scope);
        Assert.AreEqual(ReportTestData.From, envelope.FromDate);
        Assert.AreEqual(ReportTestData.To, envelope.ToDate);
        Assert.AreEqual("Pending", envelope.Status);
        Assert.AreEqual(ReportTestData.Now.UtcDateTime, envelope.CreatedAt);
        Assert.IsNull(envelope.StartedAt);
        Assert.IsNull(envelope.CompletedAt);
        Assert.IsNull(envelope.Result);
        Assert.IsNull(envelope.Error);
    }

    /// <summary>The state decides whether the envelope has a result: a report that is not completed shows none, even if a result happens to be saved with it.</summary>
    /// <param name="status">The state of the report.</param>
    [TestMethod]
    [DataRow(ReportStatus.Pending)]
    [DataRow(ReportStatus.Processing)]
    [DataRow(ReportStatus.Failed)]
    public void From_ReportNotCompletedWithAResultSaved_HasNoResult(ReportStatus status)
    {
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, status);
        report.ResultJson = ReportResultSerializer.Serialize(ReportTestData.MineSummary());

        ReportResponse envelope = ReportResponse.From(report);

        Assert.AreEqual(status.ToString(), envelope.Status);
        Assert.IsNull(envelope.Result);
    }

    /// <summary>A processing report has the start instant and still no result.</summary>
    [TestMethod]
    public void From_ProcessingReport_HasTheStartInstantAndNoResult()
    {
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Processing);
        report.StartedAtUtc = ReportTestData.Now.AddSeconds(1);

        ReportResponse envelope = ReportResponse.From(report);

        Assert.AreEqual("Processing", envelope.Status);
        Assert.AreEqual(report.StartedAtUtc.Value.UtcDateTime, envelope.StartedAt);
        Assert.IsNull(envelope.CompletedAt);
        Assert.IsNull(envelope.Result);
        Assert.IsNull(envelope.Error);
    }

    /// <summary>A completed report has the completion instant and the saved result, and no error.</summary>
    [TestMethod]
    public void From_CompletedReport_HasTheCompletionInstantAndTheSavedResult()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);

        ReportResponse envelope = ReportResponse.From(report);

        Assert.AreEqual("Completed", envelope.Status);
        Assert.AreEqual(report.CompletedAtUtc!.Value.UtcDateTime, envelope.CompletedAt);
        Assert.IsNull(envelope.Error);
        ReportResultResponse result = envelope.Result!;
        Assert.AreEqual(ReportTestData.Now.UtcDateTime, result.GeneratedAt);
        Assert.AreEqual(3, result.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("175.00"), result.TotalAmount);
        string[] statuses = ["Draft", "Submitted", "Approved", "Rejected", "Paid"];
        CollectionAssert.AreEqual(statuses, result.ByStatus.Select(total => total.Status).ToArray());
        Assert.AreEqual(2, result.ByStatus.Single(total => total.Status == "Approved").Count);
        Assert.AreEqual(ReportTestData.Dec("150.00"), result.ByStatus.Single(total => total.Status == "Approved").Amount);
    }

    /// <summary>A failed report has the completion instant and the error, and no result.</summary>
    [TestMethod]
    public void From_FailedReport_HasTheCompletionInstantAndTheErrorAndNoResult()
    {
        ReportRequest report = ReportTestData.FailedReport(ReportTestData.Requester);

        ReportResponse envelope = ReportResponse.From(report);

        Assert.AreEqual("Failed", envelope.Status);
        Assert.IsNotNull(envelope.CompletedAt);
        Assert.IsNull(envelope.Result);
        Assert.AreEqual("InvalidRequest", envelope.Error!.Code);
        Assert.AreEqual("The report could not be processed.", envelope.Error.Message);
    }

    /// <summary>A global report shows the scope Global.</summary>
    [TestMethod]
    public void From_GlobalReport_ShowsTheScopeGlobal()
    {
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, scope: ReportScope.Global);

        Assert.AreEqual("Global", ReportResponse.From(report).Scope);
    }

    /// <summary>A completed report whose saved result cannot be read is an error and not an empty result.</summary>
    [TestMethod]
    public void From_CompletedReportWithAnUnreadableResult_Throws()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        report.ResultJson = "{}";

        Assert.Throws<InvalidOperationException>(() => ReportResponse.From(report));
    }

    /// <summary>The JSON of a pending report has exactly the names of the contract, with null where there is nothing yet.</summary>
    [TestMethod]
    public void Json_PendingReport_HasTheNamesOfTheContract()
    {
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(ReportResponse.From(report), _mvcOptions));
        JsonElement root = document.RootElement;

        string[] names = ["id", "scope", "fromDate", "toDate", "status", "createdAt", "startedAt", "completedAt", "result", "error"];
        CollectionAssert.AreEquivalent(names, root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(report.Id.ToString(), root.GetProperty("id").GetString());
        Assert.AreEqual("2026-09-01", root.GetProperty("fromDate").GetString());
        Assert.AreEqual("2026-09-30", root.GetProperty("toDate").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("startedAt").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("completedAt").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("result").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("error").ValueKind);
    }

    /// <summary>The instants are written in UTC, ending in Z, as in the contract.</summary>
    [TestMethod]
    public void Json_Instants_EndInZ()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(ReportResponse.From(report), _mvcOptions));
        JsonElement root = document.RootElement;

        Assert.AreEqual("2026-10-02T15:00:00Z", root.GetProperty("createdAt").GetString());
        Assert.AreEqual("2026-10-02T15:00:01Z", root.GetProperty("startedAt").GetString());
        Assert.AreEqual("2026-10-02T15:00:02Z", root.GetProperty("completedAt").GetString());
        Assert.AreEqual("2026-10-02T15:00:00Z", root.GetProperty("result").GetProperty("generatedAt").GetString());
    }

    /// <summary>The JSON of a completed report has the result with the five states, as numbers.</summary>
    [TestMethod]
    public void Json_CompletedReport_HasTheResultOfTheContract()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(ReportResponse.From(report), _mvcOptions));
        JsonElement result = document.RootElement.GetProperty("result");

        string[] resultNames = ["generatedAt", "totalCount", "totalAmount", "byStatus"];
        CollectionAssert.AreEquivalent(resultNames, result.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(3, result.GetProperty("totalCount").GetInt32());
        Assert.AreEqual(ReportTestData.Dec("175.00"), result.GetProperty("totalAmount").GetDecimal());
        JsonElement[] states = result.GetProperty("byStatus").EnumerateArray().ToArray();
        Assert.HasCount(5, states);
        string[] stateNames = ["status", "count", "amount"];
        CollectionAssert.AreEquivalent(stateNames, states[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("Paid", states[4].GetProperty("status").GetString());
        Assert.AreEqual(ReportTestData.Dec("25.00"), states[4].GetProperty("amount").GetDecimal());
    }

    /// <summary>The JSON of a failed report has the error with a code and a message, and a null result.</summary>
    [TestMethod]
    public void Json_FailedReport_HasTheErrorOfTheContract()
    {
        ReportRequest report = ReportTestData.FailedReport(ReportTestData.Requester);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(ReportResponse.From(report), _mvcOptions));
        JsonElement root = document.RootElement;

        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("result").ValueKind);
        string[] errorNames = ["code", "message"];
        CollectionAssert.AreEquivalent(errorNames, root.GetProperty("error").EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("InvalidRequest", root.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>The envelope does not accept a missing request.</summary>
    [TestMethod]
    public void From_MissingReport_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportResponse.From(null!));
    }
}
