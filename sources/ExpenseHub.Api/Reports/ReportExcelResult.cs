using System;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The possible outcomes of downloading the Excel file of a report.
/// </summary>
public enum ReportExcelStatus
{
    /// <summary>The report is the caller's own and completed, so the file is ready.</summary>
    Ready,

    /// <summary>The report does not exist or belongs to someone else. Both look the same.</summary>
    NotFound,

    /// <summary>The report is the caller's own but is not completed (pending, processing or failed).</summary>
    NotCompleted,
}

/// <summary>
/// The result of downloading the Excel file of a report.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="FileName">The name of the file, when the status is <see cref="ReportExcelStatus.Ready"/>.</param>
/// <param name="Content">The bytes of the file, when the status is <see cref="ReportExcelStatus.Ready"/>.</param>
public sealed record ReportExcelResult(ReportExcelStatus Status, string? FileName, ReadOnlyMemory<byte> Content)
{
    /// <summary>Builds the result of a file that is ready.</summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The bytes of the file.</param>
    /// <returns>The result.</returns>
    public static ReportExcelResult Ready(string fileName, ReadOnlyMemory<byte> content) =>
        new(ReportExcelStatus.Ready, fileName, content);

    /// <summary>Builds the result for a report that does not exist or belongs to someone else.</summary>
    /// <returns>The result.</returns>
    public static ReportExcelResult NotFound() =>
        new(ReportExcelStatus.NotFound, null, ReadOnlyMemory<byte>.Empty);

    /// <summary>Builds the result for a report that is not completed.</summary>
    /// <returns>The result.</returns>
    public static ReportExcelResult NotCompleted() =>
        new(ReportExcelStatus.NotCompleted, null, ReadOnlyMemory<byte>.Empty);
}
