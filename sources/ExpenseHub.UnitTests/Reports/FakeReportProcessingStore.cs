using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Hand-written fake of <see cref="IReportProcessingStore"/>; it never touches a database. It keeps the saved requests in
/// memory and imitates the conditional transitions of the real store: a start applies only to a pending request, a completion only
/// to a processing one, a failure to a request that is not finished, and a completion writes the state, the instant and the
/// result in one step. It records every call, can be told to fail as if the database were unavailable, and can run a hook right
/// before a transition to imitate another worker that moved the request first. Its state outlives any processor, so a second
/// processor on the same store imitates a restart of the worker.
/// </summary>
internal sealed class FakeReportProcessingStore : IReportProcessingStore
{
    private readonly Dictionary<Guid, ReportRequest> _reports = new();
    private readonly List<string> _calls = new();

    /// <summary>Gets the names of the calls, in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Gets how many writes were applied: a start, a completion or a failure that changed a request.</summary>
    public int AppliedWrites { get; private set; }

    /// <summary>Gets or sets how many of the next reads fail as if the database were unavailable.</summary>
    public int FindFailures { get; set; }

    /// <summary>Gets or sets how many of the next starts fail as if the database were unavailable.</summary>
    public int StartFailures { get; set; }

    /// <summary>Gets or sets how many of the next completions fail as if the database were unavailable.</summary>
    public int CompleteFailures { get; set; }

    /// <summary>Gets or sets how many of the next failures fail as if the database were unavailable.</summary>
    public int FailFailures { get; set; }

    /// <summary>Gets or sets a value indicating whether every start is refused without changing the request, to imitate a write that does not apply.</summary>
    public bool RefuseStarts { get; set; }

    /// <summary>Gets or sets what runs right before a start, to imitate another worker.</summary>
    public Action<ReportRequest>? BeforeStart { get; set; }

    /// <summary>Gets or sets what runs right before a completion, to imitate another worker.</summary>
    public Action<ReportRequest>? BeforeComplete { get; set; }

    /// <summary>Counts the calls of a kind.</summary>
    /// <param name="name">The name of the call.</param>
    /// <returns>How many times it was called.</returns>
    public int CallsOf(string name)
    {
        int count = 0;

        foreach (string call in _calls)
        {
            if (call == name)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Saves a request as it is.</summary>
    /// <param name="report">The request.</param>
    public void Given(ReportRequest report)
    {
        _reports[report.Id] = Copy(report);
    }

    /// <summary>Gets a copy of the request as it is saved now.</summary>
    /// <param name="reportId">The identifier of the request.</param>
    /// <returns>The request.</returns>
    public ReportRequest Saved(Guid reportId)
    {
        return Copy(_reports[reportId]);
    }

    /// <inheritdoc />
    public Task<ReportRequest?> FindAsync(Guid reportId)
    {
        _calls.Add(nameof(FindAsync));
        FailIfTold(() => FindFailures, value => FindFailures = value);

        return Task.FromResult(_reports.TryGetValue(reportId, out ReportRequest? report) ? Copy(report) : null);
    }

    /// <inheritdoc />
    public Task<bool> TryStartAsync(Guid reportId, DateTimeOffset startedAtUtc)
    {
        _calls.Add(nameof(TryStartAsync));
        FailIfTold(() => StartFailures, value => StartFailures = value);

        if (_reports.TryGetValue(reportId, out ReportRequest? report))
        {
            BeforeStart?.Invoke(report);
        }

        if (RefuseStarts || report is null || report.Status != ReportStatus.Pending)
        {
            return Task.FromResult(false);
        }

        report.Status = ReportStatus.Processing;
        report.StartedAtUtc = startedAtUtc;
        AppliedWrites++;

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> TryCompleteAsync(Guid reportId, string resultJson, DateTimeOffset completedAtUtc)
    {
        _calls.Add(nameof(TryCompleteAsync));
        FailIfTold(() => CompleteFailures, value => CompleteFailures = value);

        if (_reports.TryGetValue(reportId, out ReportRequest? report))
        {
            BeforeComplete?.Invoke(report);
        }

        if (report is null || report.Status != ReportStatus.Processing)
        {
            return Task.FromResult(false);
        }

        report.Status = ReportStatus.Completed;
        report.CompletedAtUtc = completedAtUtc;
        report.ResultJson = resultJson;
        AppliedWrites++;

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> TryFailAsync(Guid reportId, string errorCode, string errorMessage, DateTimeOffset completedAtUtc)
    {
        _calls.Add(nameof(TryFailAsync));
        FailIfTold(() => FailFailures, value => FailFailures = value);

        if (!_reports.TryGetValue(reportId, out ReportRequest? report) || report.Status is not (ReportStatus.Pending or ReportStatus.Processing))
        {
            return Task.FromResult(false);
        }

        report.Status = ReportStatus.Failed;
        report.CompletedAtUtc = completedAtUtc;
        report.ErrorCode = errorCode;
        report.ErrorMessage = errorMessage;
        AppliedWrites++;

        return Task.FromResult(true);
    }

    private static ReportRequest Copy(ReportRequest report)
    {
        return new ReportRequest
        {
            Id = report.Id,
            RequesterId = report.RequesterId,
            Scope = report.Scope,
            FromDate = report.FromDate,
            ToDate = report.ToDate,
            Status = report.Status,
            CreatedAtUtc = report.CreatedAtUtc,
            StartedAtUtc = report.StartedAtUtc,
            CompletedAtUtc = report.CompletedAtUtc,
            ResultJson = report.ResultJson,
            ErrorCode = report.ErrorCode,
            ErrorMessage = report.ErrorMessage,
        };
    }

    private static void FailIfTold(Func<int> remaining, Action<int> set)
    {
        if (remaining() > 0)
        {
            set(remaining() - 1);

            throw new ReportStorageException("Simulated storage failure.", new InvalidOperationException("The database is unavailable."));
        }
    }
}
