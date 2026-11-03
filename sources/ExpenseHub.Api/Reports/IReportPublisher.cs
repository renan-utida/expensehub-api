using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Whether the broker confirmed that it received the message of a report.
/// </summary>
public enum ReportPublishResult
{
    /// <summary>The broker confirmed the message. Only then can the request be accepted.</summary>
    Confirmed,

    /// <summary>The broker did not confirm the message, so it may not exist. The request must not be accepted.</summary>
    NotConfirmed,
}

/// <summary>
/// Sends the message <c>{"reportId": "..."}</c> to the queue of the worker. The broker sits behind this interface, so the
/// rules of the service are tested with a fake and never with a real broker.
/// </summary>
public interface IReportPublisher
{
    /// <summary>
    /// Publishes the message of a report that is already saved.
    /// </summary>
    /// <param name="reportId">The identifier of the saved request. It is the only content of the message.</param>
    /// <param name="cancellationToken">Cancels the publication.</param>
    /// <returns><see cref="ReportPublishResult.Confirmed"/> only when the broker confirmed the message.</returns>
    /// <exception cref="ReportPublishException">The broker could not be reached; it is treated as not confirmed.</exception>
    Task<ReportPublishResult> PublishAsync(Guid reportId, CancellationToken cancellationToken);
}
