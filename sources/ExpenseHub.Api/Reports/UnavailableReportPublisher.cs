using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The publisher used while no real broker publisher is registered. It never confirms, so a request is never accepted
/// without a message that was really sent: the API answers <c>503</c> instead of pretending. The web application registers
/// <see cref="RabbitMqReportPublisher"/> in its place.
/// </summary>
public sealed partial class UnavailableReportPublisher : IReportPublisher
{
    private readonly ILogger<UnavailableReportPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnavailableReportPublisher"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public UnavailableReportPublisher(ILogger<UnavailableReportPublisher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ReportPublishResult> PublishAsync(Guid reportId, CancellationToken cancellationToken)
    {
        LogNoPublisher(reportId);

        return Task.FromResult(ReportPublishResult.NotConfirmed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} was not published: no message broker publisher is configured.")]
    private partial void LogNoPublisher(Guid reportId);
}
