using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Reports;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Hand-written fake of <see cref="IReportPublisher"/>; it never reaches a broker. It confirms by default, can be told not to
/// confirm or to fail, and records every identifier it was asked to publish. When it is given the repository, it also
/// records whether the request was already saved when the message was published.
/// </summary>
internal sealed class FakeReportPublisher : IReportPublisher
{
    private readonly List<Guid> _published = new();
    private readonly FakeReportRepository? _repository;

    /// <summary>Initializes a new instance of the <see cref="FakeReportPublisher"/> class.</summary>
    /// <param name="repository">The repository to look into when a message is published, or <c>null</c>.</param>
    public FakeReportPublisher(FakeReportRepository? repository = null)
    {
        _repository = repository;
    }

    /// <summary>Gets or sets what the broker answers.</summary>
    public ReportPublishResult Result { get; set; } = ReportPublishResult.Confirmed;

    /// <summary>Gets or sets a value indicating whether publishing fails as if the broker could not be reached.</summary>
    public bool ThrowOnPublish { get; set; }

    /// <summary>Gets the identifiers that were published, in order.</summary>
    public IReadOnlyList<Guid> Published => _published;

    /// <summary>Gets a value indicating whether the request was already saved at the last publication.</summary>
    public bool SavedAtLastPublish { get; private set; }

    /// <inheritdoc />
    public Task<ReportPublishResult> PublishAsync(Guid reportId, CancellationToken cancellationToken)
    {
        _published.Add(reportId);

        if (_repository is not null)
        {
            SavedAtLastPublish = _repository.Reports.Any(report => report.Id == reportId);
        }

        if (ThrowOnPublish)
        {
            throw new ReportPublishException("Simulated broker failure.", new InvalidOperationException("The broker is unreachable."));
        }

        return Task.FromResult(Result);
    }
}
