using System;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExpenseHub.Api.Domain.Entities;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Builds the Excel file (<c>.xlsx</c>) of a completed report with the Open XML SDK (MIT licence). It is built in memory from
/// the result that was saved, so it does not read any expense again and needs no message. One sheet holds the identifier,
/// the scope, the period and the generation instant, then a table with the five states (count and amount) and the grand
/// total. The counts and the amounts are numeric cells; the dates and the instant are text in ISO form.
/// </summary>
public static class ReportExcelWriter
{
    /// <summary>The content type of an <c>.xlsx</c> file.</summary>
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The name of the only sheet.</summary>
    public const string SheetName = "Resumo";

    private const uint AmountStyle = 1U;

    /// <summary>
    /// Gets the name of the file of a report.
    /// </summary>
    /// <param name="reportId">The identifier of the report.</param>
    /// <returns>The name, in the form <c>report-{id}.xlsx</c>.</returns>
    public static string FileNameFor(Guid reportId)
    {
        return $"report-{reportId}.xlsx";
    }

    /// <summary>
    /// Builds the file.
    /// </summary>
    /// <param name="report">The report, which gives the identifier, the scope and the period.</param>
    /// <param name="summary">The saved result, read from the report.</param>
    /// <returns>The bytes of the <c>.xlsx</c> file.</returns>
    public static byte[] Build(ReportRequest report, ReportSummary summary)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(summary);

        using var stream = new MemoryStream();

        using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            WorkbookStylesPart stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();

            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(CreateColumns(), CreateRows(report, summary));

            workbookPart.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = SheetName,
            }));
        }

        return stream.ToArray();
    }

    private static Stylesheet CreateStylesheet()
    {
        // Style 0 is the default; style 1 shows a number with two decimal places (the built-in format 2, "0.00").
        var fonts = new Fonts(new Font()) { Count = 1U };
        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        fills.Count = 2U;
        var borders = new Borders(new Border()) { Count = 1U };
        var styleFormats = new CellStyleFormats(new CellFormat()) { Count = 1U };
        var cellFormats = new CellFormats(
            new CellFormat(),
            new CellFormat { NumberFormatId = 2U, ApplyNumberFormat = true });
        cellFormats.Count = 2U;

        return new Stylesheet(fonts, fills, borders, styleFormats, cellFormats);
    }

    private static Columns CreateColumns()
    {
        return new Columns(
            new Column { Min = 1U, Max = 1U, Width = 20D, CustomWidth = true },
            new Column { Min = 2U, Max = 2U, Width = 40D, CustomWidth = true },
            new Column { Min = 3U, Max = 3U, Width = 16D, CustomWidth = true });
    }

    private static SheetData CreateRows(ReportRequest report, ReportSummary summary)
    {
        var rows = new SheetData();
        uint row = 1U;

        rows.Append(LabelRow(row++, "Identificador", report.Id.ToString()));
        rows.Append(LabelRow(row++, "Escopo", report.Scope.ToString()));
        rows.Append(LabelRow(row++, "Data inicial", report.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        rows.Append(LabelRow(row++, "Data final", report.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        rows.Append(LabelRow(row++, "Gerado em (UTC)", summary.GeneratedAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));

        row++;
        rows.Append(new Row(TextCell("A", row, "Estado"), TextCell("B", row, "Quantidade"), TextCell("C", row, "Valor")) { RowIndex = row });
        row++;

        foreach (ReportStatusTotal total in summary.ByStatus)
        {
            rows.Append(TableRow(row++, total.Status.ToString(), total.Count, total.Amount));
        }

        rows.Append(TableRow(row, "Total geral", summary.TotalCount, summary.TotalAmount));

        return rows;
    }

    private static Row LabelRow(uint row, string label, string value)
    {
        return new Row(TextCell("A", row, label), TextCell("B", row, value)) { RowIndex = row };
    }

    private static Row TableRow(uint row, string label, int count, decimal amount)
    {
        return new Row(TextCell("A", row, label), CountCell("B", row, count), AmountCell("C", row, amount)) { RowIndex = row };
    }

    private static Cell TextCell(string column, uint row, string text)
    {
        return new Cell
        {
            CellReference = column + row.ToString(CultureInfo.InvariantCulture),
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(text)),
        };
    }

    private static Cell CountCell(string column, uint row, int count)
    {
        return new Cell
        {
            CellReference = column + row.ToString(CultureInfo.InvariantCulture),
            DataType = CellValues.Number,
            CellValue = new CellValue(count.ToString(CultureInfo.InvariantCulture)),
        };
    }

    private static Cell AmountCell(string column, uint row, decimal amount)
    {
        return new Cell
        {
            CellReference = column + row.ToString(CultureInfo.InvariantCulture),
            DataType = CellValues.Number,
            StyleIndex = AmountStyle,
            CellValue = new CellValue(amount.ToString("0.00", CultureInfo.InvariantCulture)),
        };
    }
}
