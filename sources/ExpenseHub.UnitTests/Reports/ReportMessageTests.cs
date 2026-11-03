using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the message that goes to the worker. It carries only the identifier of the report, and the body of a message is
/// untrusted: whatever is not a JSON object with a non-empty identifier is refused, and extra properties are ignored, so the
/// content of a message can never decide who a report is for.
/// </summary>
[TestClass]
public sealed class ReportMessageTests
{
    private const string SampleId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    /// <summary>The message is exactly one property, <c>reportId</c>, with the identifier in its usual form with hyphens.</summary>
    [TestMethod]
    public void Serialize_WritesOnlyTheReportId()
    {
        var id = Guid.Parse(SampleId);

        string json = Encoding.UTF8.GetString(ReportMessage.Serialize(id));

        Assert.AreEqual($"{{\"reportId\":\"{SampleId}\"}}", json);
    }

    /// <summary>The message has a single property, so the requester, the scope and the period cannot travel in it.</summary>
    [TestMethod]
    public void Serialize_HasExactlyOneProperty()
    {
        using JsonDocument document = JsonDocument.Parse(ReportMessage.Serialize(Guid.NewGuid()));

        string[] names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        string[] expected = ["reportId"];
        CollectionAssert.AreEqual(expected, names);
    }

    /// <summary>What is written can be read back to the same identifier.</summary>
    [TestMethod]
    public void TryParse_ReadsWhatSerializeWrote()
    {
        var id = Guid.NewGuid();

        bool parsed = ReportMessage.TryParse(ReportMessage.Serialize(id), out Guid read);

        Assert.IsTrue(parsed);
        Assert.AreEqual(id, read);
    }

    /// <summary>Anything that is not a JSON object with a valid, non-empty identifier is refused, and no identifier comes out.</summary>
    /// <param name="body">The text of the body.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not json")]
    [DataRow("{")]
    [DataRow("{}")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("123")]
    [DataRow("\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"")]
    [DataRow("{\"reportId\":null}")]
    [DataRow("{\"reportId\":123}")]
    [DataRow("{\"reportId\":true}")]
    [DataRow("{\"reportId\":[\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"]}")]
    [DataRow("{\"reportId\":\"\"}")]
    [DataRow("{\"reportId\":\"rel-001\"}")]
    [DataRow("{\"reportId\":\"not-a-guid\"}")]
    [DataRow("{\"reportId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [DataRow("{\"reportId\":\"3fa85f6457174562b3fc2c963f66afa6\"}")]
    [DataRow("{\"ReportId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"}")]
    [DataRow("{\"id\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"}")]
    public void TryParse_InvalidBody_IsRefused(string body)
    {
        bool parsed = ReportMessage.TryParse(Encoding.UTF8.GetBytes(body), out Guid id);

        Assert.IsFalse(parsed);
        Assert.AreEqual(Guid.Empty, id);
    }

    /// <summary>An empty body is refused.</summary>
    [TestMethod]
    public void TryParse_EmptyBody_IsRefused()
    {
        Assert.IsFalse(ReportMessage.TryParse(ReadOnlyMemory<byte>.Empty, out Guid id));
        Assert.AreEqual(Guid.Empty, id);
    }

    /// <summary>Extra properties are ignored: the requester, the scope or the owner in a message change nothing, and only the identifier is read.</summary>
    [TestMethod]
    public void TryParse_ExtraProperties_AreIgnored()
    {
        string body = $"{{\"reportId\":\"{SampleId}\",\"requesterId\":\"someone-else\",\"ownerId\":\"someone-else\",\"scope\":\"Global\",\"fromDate\":\"2000-01-01\"}}";

        bool parsed = ReportMessage.TryParse(Encoding.UTF8.GetBytes(body), out Guid id);

        Assert.IsTrue(parsed);
        Assert.AreEqual(Guid.Parse(SampleId), id);
    }

    /// <summary>The identifier is read from the body whatever the surrounding spaces and line breaks are.</summary>
    [TestMethod]
    public void TryParse_SpacesAndLineBreaks_AreAccepted()
    {
        string body = $"  {{\n  \"reportId\" : \"{SampleId}\"\n}}  ";

        Assert.IsTrue(ReportMessage.TryParse(Encoding.UTF8.GetBytes(body), out Guid id));
        Assert.AreEqual(Guid.Parse(SampleId), id);
    }
}
