using System;
using System.Buffers;
using System.Text.Json;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The message that goes from the web application to the worker: <c>{"reportId": "..."}</c>, and nothing else. The period, the
/// scope and the requester are never in it, because the worker reads them from the request that was saved. Anything else
/// that arrives in a message is ignored, so the content of a message cannot change who a report is for or what it adds up.
/// </summary>
public static class ReportMessage
{
    /// <summary>The name of the only property of the message.</summary>
    public const string ReportIdProperty = "reportId";

    /// <summary>
    /// Writes the message of a report.
    /// </summary>
    /// <param name="reportId">The identifier of the saved request.</param>
    /// <returns>The UTF-8 JSON of the message, with the identifier in its usual form with hyphens.</returns>
    public static byte[] Serialize(Guid reportId)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(ReportIdProperty, reportId.ToString("D"));
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Reads the identifier from the body of a message. The body is untrusted: anything that is not a JSON object with a
    /// <c>reportId</c> that is a non-empty identifier is refused, and every other property is ignored.
    /// </summary>
    /// <param name="body">The body of the message.</param>
    /// <param name="reportId">The identifier, when the body is a valid message.</param>
    /// <returns><c>true</c> when the body is a valid message.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> body, out Guid reportId)
    {
        reportId = Guid.Empty;

        if (body.IsEmpty)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(ReportIdProperty, out JsonElement property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!Guid.TryParseExact(property.GetString(), "D", out Guid parsed) || parsed == Guid.Empty)
            {
                return false;
            }

            reportId = parsed;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
