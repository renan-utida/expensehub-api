using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the body of <c>POST /api/reports</c>: its data annotations, the fact that it has no field the client must not set
/// (the requester comes from the token), and how the JSON is read with the options MVC uses. An invalid body is a 400 before
/// the service is called, so nothing is saved or published.
/// </summary>
[TestClass]
public sealed class CreateReportRequestTests
{
    private static readonly JsonSerializerOptions _mvcOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The request has only the three fields the client controls: no requester, owner, state or instant to assign.</summary>
    [TestMethod]
    public void CreateReportRequest_HasOnlyTheDatesAndTheScope()
    {
        string[] members = typeof(CreateReportRequest).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        string[] expected = ["FromDate", "Scope", "ToDate"];
        CollectionAssert.AreEqual(expected, members);
    }

    /// <summary>Valid data passes the annotations.</summary>
    [TestMethod]
    public void Validate_ValidData_HasNoErrors()
    {
        Assert.IsEmpty(Validate(Request(ReportTestData.From, ReportTestData.To, "Mine")));
        Assert.IsEmpty(Validate(Request(ReportTestData.From, ReportTestData.To, "Global")));
    }

    /// <summary>A period of a single day passes.</summary>
    [TestMethod]
    public void Validate_PeriodOfOneDay_HasNoErrors()
    {
        Assert.IsEmpty(Validate(Request(ReportTestData.From, ReportTestData.From, "Mine")));
    }

    /// <summary>A missing start date, end date or scope is an error on its own field, so an omitted field is never read as a default.</summary>
    [TestMethod]
    public void Validate_MissingFields_ReportEachField()
    {
        CollectionAssert.Contains(Validate(Request(null, ReportTestData.To, "Mine")), nameof(CreateReportRequest.FromDate));
        CollectionAssert.Contains(Validate(Request(ReportTestData.From, null, "Mine")), nameof(CreateReportRequest.ToDate));
        CollectionAssert.Contains(Validate(Request(ReportTestData.From, ReportTestData.To, null)), nameof(CreateReportRequest.Scope));
    }

    /// <summary>A start date after the end date is an error.</summary>
    [TestMethod]
    public void Validate_StartAfterEnd_ReportsTheStartDate()
    {
        CollectionAssert.Contains(Validate(Request(ReportTestData.To, ReportTestData.From, "Mine")), nameof(CreateReportRequest.FromDate));
    }

    /// <summary>A scope that is not exactly Mine or Global is an error.</summary>
    /// <param name="scope">The text.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("0")]
    [DataRow("mine")]
    [DataRow("Everyone")]
    public void Validate_InvalidScope_ReportsTheScope(string scope)
    {
        CollectionAssert.Contains(Validate(Request(ReportTestData.From, ReportTestData.To, scope)), nameof(CreateReportRequest.Scope));
    }

    /// <summary>A complete body is read with the dates and the scope as sent.</summary>
    [TestMethod]
    public void Json_CompleteBody_IsRead()
    {
        CreateReportRequest request = Read("{\"fromDate\":\"2026-09-01\",\"toDate\":\"2026-09-30\",\"scope\":\"Mine\"}");

        Assert.AreEqual(new DateOnly(2026, 9, 1), request.FromDate);
        Assert.AreEqual(new DateOnly(2026, 9, 30), request.ToDate);
        Assert.AreEqual("Mine", request.Scope);
        Assert.IsEmpty(Validate(request));
    }

    /// <summary>A body without fields has every field empty and fails the validation, so a missing date is not the first day of the calendar and a missing scope is not Mine.</summary>
    [TestMethod]
    public void Json_EmptyBody_LeavesEveryFieldEmptyAndInvalid()
    {
        CreateReportRequest request = Read("{}");

        Assert.IsNull(request.FromDate);
        Assert.IsNull(request.ToDate);
        Assert.IsNull(request.Scope);
        Assert.HasCount(3, Validate(request));
    }

    /// <summary>A scope sent as a number is refused when the body is read, so it never becomes Mine or Global by its position in the enum.</summary>
    /// <param name="scopeJson">The JSON of the scope.</param>
    [TestMethod]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("2")]
    [DataRow("true")]
    public void Json_ScopeAsAValueThatIsNotText_IsRefused(string scopeJson)
    {
        string json = "{\"fromDate\":\"2026-09-01\",\"toDate\":\"2026-09-30\",\"scope\":" + scopeJson + "}";

        Assert.Throws<JsonException>(() => Read(json));
    }

    /// <summary>A scope sent as a number inside quotes is read as text and then refused by the validation.</summary>
    [TestMethod]
    public void Json_ScopeAsANumberInQuotes_FailsTheValidation()
    {
        CreateReportRequest request = Read("{\"fromDate\":\"2026-09-01\",\"toDate\":\"2026-09-30\",\"scope\":\"0\"}");

        CollectionAssert.Contains(Validate(request), nameof(CreateReportRequest.Scope));
    }

    /// <summary>A date that does not exist in the calendar is refused when the body is read.</summary>
    /// <param name="date">The text of the date.</param>
    [TestMethod]
    [DataRow("2026-02-30")]
    [DataRow("2026-13-01")]
    [DataRow("30/09/2026")]
    [DataRow("not a date")]
    public void Json_InvalidDate_IsRefused(string date)
    {
        string json = "{\"fromDate\":\"" + date + "\",\"toDate\":\"2026-09-30\",\"scope\":\"Mine\"}";

        Assert.Throws<JsonException>(() => Read(json));
    }

    /// <summary>A requester or an owner sent in the body is ignored: the request has no member to receive it.</summary>
    [TestMethod]
    public void Json_RequesterAndOwnerInTheBody_AreIgnored()
    {
        const string json = "{\"fromDate\":\"2026-09-01\",\"toDate\":\"2026-09-30\",\"scope\":\"Mine\",\"requesterId\":\"someone-else\",\"ownerId\":\"someone-else\",\"status\":\"Completed\"}";

        CreateReportRequest request = Read(json);

        Assert.IsEmpty(Validate(request));
        Assert.DoesNotContain("someone-else", JsonSerializer.Serialize(request, _mvcOptions));
    }

    private static CreateReportRequest Request(DateOnly? fromDate, DateOnly? toDate, string? scope)
    {
        return new CreateReportRequest { FromDate = fromDate, ToDate = toDate, Scope = scope };
    }

    private static CreateReportRequest Read(string json)
    {
        return JsonSerializer.Deserialize<CreateReportRequest>(json, _mvcOptions)!;
    }

    private static string[] Validate(CreateReportRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).ToArray();
    }
}
