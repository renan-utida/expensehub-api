using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the Excel file of a report. The file is built in memory and opened again with the same library, so the tests prove
/// that it is a real spreadsheet (not text with another name), that it is valid, that the headers and the five states are
/// right, and that the counts and the amounts are numeric cells equal to the saved result, zeros included.
/// </summary>
[TestClass]
public sealed class ReportExcelWriterTests
{
    private static readonly string[] _statusNames = ["Draft", "Submitted", "Approved", "Rejected", "Paid"];

    /// <summary>The name of the file follows the contract of the download: report-{id}.xlsx. The content type is checked in the controller tests, on the file that is returned.</summary>
    [TestMethod]
    public void FileName_FollowsTheContract()
    {
        var id = Guid.NewGuid();

        Assert.AreEqual($"report-{id}.xlsx", ReportExcelWriter.FileNameFor(id));
    }

    /// <summary>The bytes are a real xlsx: a ZIP package with the parts of a workbook, not text with another name.</summary>
    [TestMethod]
    public void Build_ProducesARealXlsxPackage()
    {
        byte[] bytes = BuildExample();

        Assert.AreEqual((byte)'P', bytes[0]);
        Assert.AreEqual((byte)'K', bytes[1]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        string[] entries = archive.Entries.Select(entry => entry.FullName).ToArray();
        CollectionAssert.Contains(entries, "[Content_Types].xml");
        CollectionAssert.Contains(entries, "xl/workbook.xml");
        Assert.IsTrue(entries.Any(entry => entry.StartsWith("xl/worksheets/", StringComparison.Ordinal)));
    }

    /// <summary>The spreadsheet opens with the library and passes the validation of the Open XML schema.</summary>
    [TestMethod]
    public void Build_OpensAndIsValid()
    {
        byte[] bytes = BuildExample();

        using SpreadsheetDocument document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var validator = new OpenXmlValidator(FileFormatVersions.Office2019);
        List<ValidationErrorInfo> errors = validator.Validate(document).ToList();

        Assert.IsEmpty(errors, string.Join("; ", errors.Select(error => error.Description)));
        WorkbookPart workbookPart = document.WorkbookPart!;
        Assert.AreEqual(1, workbookPart.WorksheetParts.Count());
        Assert.AreEqual(ReportExcelWriter.SheetName, workbookPart.Workbook!.Descendants<Sheet>().Single().Name!.Value);
    }

    /// <summary>The sheet gives the identifier, the scope, the period and the instant of generation.</summary>
    [TestMethod]
    public void Build_HasTheIdentifierTheScopeThePeriodAndTheInstant()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        ReportSummary summary = ReportResultSerializer.Deserialize(report.ResultJson!);

        Dictionary<string, SheetCell> cells = ReadCells(ReportExcelWriter.Build(report, summary));

        Assert.AreEqual("Identificador", cells["A1"].Text);
        Assert.AreEqual(report.Id.ToString(), cells["B1"].Text);
        Assert.AreEqual("Escopo", cells["A2"].Text);
        Assert.AreEqual("Mine", cells["B2"].Text);
        Assert.AreEqual("Data inicial", cells["A3"].Text);
        Assert.AreEqual("2026-09-01", cells["B3"].Text);
        Assert.AreEqual("Data final", cells["A4"].Text);
        Assert.AreEqual("2026-09-30", cells["B4"].Text);
        Assert.AreEqual("Gerado em (UTC)", cells["A5"].Text);
        Assert.AreEqual("2026-10-02T15:00:00Z", cells["B5"].Text);
    }

    /// <summary>The table has its headers, the five states in order, and the grand total.</summary>
    [TestMethod]
    public void Build_HasTheHeadersTheFiveStatesInOrderAndTheGrandTotal()
    {
        Dictionary<string, SheetCell> cells = ReadCells(BuildExample());

        Assert.AreEqual("Estado", cells["A7"].Text);
        Assert.AreEqual("Quantidade", cells["B7"].Text);
        Assert.AreEqual("Valor", cells["C7"].Text);

        for (int index = 0; index < _statusNames.Length; index++)
        {
            Assert.AreEqual(_statusNames[index], cells["A" + (8 + index).ToString(CultureInfo.InvariantCulture)].Text);
        }

        Assert.AreEqual("Total geral", cells["A13"].Text);
    }

    /// <summary>The counts and the amounts are numeric cells with the same numbers as the saved result.</summary>
    [TestMethod]
    public void Build_CountsAndAmountsAreNumericCellsEqualToTheSavedResult()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        ReportSummary summary = ReportResultSerializer.Deserialize(report.ResultJson!);

        Dictionary<string, SheetCell> cells = ReadCells(ReportExcelWriter.Build(report, summary));

        for (int index = 0; index < summary.ByStatus.Count; index++)
        {
            int row = 8 + index;
            AssertNumber(cells, "B" + row.ToString(CultureInfo.InvariantCulture), summary.ByStatus[index].Count);
            AssertNumber(cells, "C" + row.ToString(CultureInfo.InvariantCulture), summary.ByStatus[index].Amount);
        }

        AssertNumber(cells, "B13", summary.TotalCount);
        AssertNumber(cells, "C13", summary.TotalAmount);
        AssertNumber(cells, "B10", 2);
        AssertNumber(cells, "C10", 150.00m);
        AssertNumber(cells, "B13", 3);
        AssertNumber(cells, "C13", 175.00m);
    }

    /// <summary>The empty result gives numeric zeros in the five states and in the total, not blanks.</summary>
    [TestMethod]
    public void Build_EmptyResult_HasNumericZeros()
    {
        ReportSummary empty = ReportTestData.Calculate([], ReportTestData.Query(ReportScope.Mine));
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Completed);

        Dictionary<string, SheetCell> cells = ReadCells(ReportExcelWriter.Build(report, empty));

        for (int row = 8; row <= 13; row++)
        {
            AssertNumber(cells, "B" + row.ToString(CultureInfo.InvariantCulture), 0);
            AssertNumber(cells, "C" + row.ToString(CultureInfo.InvariantCulture), 0m);
        }
    }

    /// <summary>The file shows the scope Global when the report is global.</summary>
    [TestMethod]
    public void Build_GlobalReport_ShowsTheScopeGlobal()
    {
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Completed, ReportScope.Global);

        Dictionary<string, SheetCell> cells = ReadCells(ReportExcelWriter.Build(report, ReportTestData.MineSummary()));

        Assert.AreEqual("Global", cells["B2"].Text);
    }

    /// <summary>The builder does not accept missing arguments.</summary>
    [TestMethod]
    public void Build_MissingArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportExcelWriter.Build(null!, ReportTestData.MineSummary()));
        Assert.Throws<ArgumentNullException>(() => ReportExcelWriter.Build(ReportTestData.Report(ReportTestData.Requester), null!));
    }

    private static byte[] BuildExample()
    {
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);

        return ReportExcelWriter.Build(report, ReportResultSerializer.Deserialize(report.ResultJson!));
    }

    private static void AssertNumber(Dictionary<string, SheetCell> cells, string reference, decimal expected)
    {
        SheetCell cell = cells[reference];

        Assert.IsTrue(cell.IsNumber, $"{reference} must be a numeric cell");
        Assert.AreEqual(expected, decimal.Parse(cell.Text, NumberStyles.Number, CultureInfo.InvariantCulture), reference);
    }

    private static Dictionary<string, SheetCell> ReadCells(byte[] bytes)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        WorksheetPart sheet = document.WorkbookPart!.WorksheetParts.Single();
        var cells = new Dictionary<string, SheetCell>();

        foreach (Cell cell in sheet.Worksheet!.Descendants<Cell>())
        {
            EnumValue<CellValues>? dataType = cell.DataType;
            bool isNumber = dataType is null || dataType.Value == CellValues.Number;
            string text = cell.InlineString?.Text?.Text ?? cell.CellValue?.Text ?? string.Empty;
            cells[cell.CellReference!.Value!] = new SheetCell(isNumber && cell.InlineString is null, text);
        }

        return cells;
    }
}

/// <summary>
/// What the tests read from a cell of the spreadsheet: whether it is numeric, and its text.
/// </summary>
/// <param name="IsNumber">Whether the cell holds a number and not text.</param>
/// <param name="Text">The text or the number, as written in the file.</param>
internal sealed record SheetCell(bool IsNumber, string Text);
