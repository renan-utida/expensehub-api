using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Persistence.Converters;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Writes and reads the result of a report as the JSON kept in <c>ReportRequest.ResultJson</c>. Money is stored as integer
/// cents, so nothing depends on floating point; the decimals come back only when the result is read. The same checks run
/// when writing and when reading: the five states appear once each, no count or amount is negative, and the totals are the
/// sums of the states. A result that breaks them is never written, and one that does not read back is an error, not a guess.
/// </summary>
public static class ReportResultSerializer
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    /// <summary>
    /// Turns a result into the JSON that is saved.
    /// </summary>
    /// <param name="summary">The result calculated by the worker.</param>
    /// <returns>The JSON text, with the money in cents.</returns>
    /// <exception cref="InvalidOperationException">The result is not coherent.</exception>
    public static string Serialize(ReportSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var stored = new StoredReportResult(
            summary.GeneratedAtUtc.ToUniversalTime(),
            summary.TotalCount,
            MoneyConversion.ToCents(summary.TotalAmount),
            summary.ByStatus
                .Select(total => new StoredReportStatusTotal(total.Status, total.Count, MoneyConversion.ToCents(total.Amount)))
                .ToList());

        EnsureCoherent(stored);

        return JsonSerializer.Serialize(stored, _options);
    }

    /// <summary>
    /// Reads the saved JSON back into a result. It never recalculates anything.
    /// </summary>
    /// <param name="json">The JSON text saved with the request.</param>
    /// <returns>The result, with decimal amounts and the five states in the order of <see cref="ExpenseStatus"/>.</returns>
    /// <exception cref="InvalidOperationException">The text is not valid JSON, or the result in it is not coherent.</exception>
    public static ReportSummary Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        StoredReportResult? stored;

        try
        {
            stored = JsonSerializer.Deserialize<StoredReportResult>(json, _options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The saved report result cannot be read.", exception);
        }

        if (stored is null)
        {
            throw new InvalidOperationException("The saved report result is empty.");
        }

        EnsureCoherent(stored);

        var byStatus = new List<ReportStatusTotal>();

        foreach (ExpenseStatus status in Enum.GetValues<ExpenseStatus>())
        {
            StoredReportStatusTotal total = stored.ByStatus.Single(candidate => candidate.Status == status);
            byStatus.Add(new ReportStatusTotal(status, total.Count, MoneyConversion.FromCents(total.AmountCents)));
        }

        return new ReportSummary(
            stored.GeneratedAtUtc,
            stored.TotalCount,
            MoneyConversion.FromCents(stored.TotalAmountCents),
            byStatus);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));

        return options;
    }

    private static void EnsureCoherent(StoredReportResult stored)
    {
        IReadOnlyList<StoredReportStatusTotal>? lines = stored.ByStatus;
        ExpenseStatus[] statuses = Enum.GetValues<ExpenseStatus>();

        if (lines is null || lines.Count != statuses.Length || statuses.Any(status => lines.Count(line => line.Status == status) != 1))
        {
            throw new InvalidOperationException("The saved report result must have each of the five states exactly once.");
        }

        if (lines.Any(line => line.Count < 0 || line.AmountCents < 0) || stored.TotalCount < 0 || stored.TotalAmountCents < 0)
        {
            throw new InvalidOperationException("The saved report result cannot have negative counts or amounts.");
        }

        if (stored.TotalCount != lines.Sum(line => line.Count) || stored.TotalAmountCents != lines.Sum(line => line.AmountCents))
        {
            throw new InvalidOperationException("The totals of the saved report result are not the sums of its states.");
        }
    }
}
