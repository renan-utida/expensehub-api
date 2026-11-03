using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IReportProcessingStore"/>. Each transition loads the request, checks that it is still in
/// the state the transition starts from, changes it and saves it in one write. The state is a concurrency token, so the write
/// carries <c>WHERE Status = &lt;the state that was read&gt;</c>: if another attempt moved the request in the meantime, nothing is
/// written and the transition returns <c>false</c>. A temporarily unavailable database (busy, locked, unreadable) becomes a
/// <see cref="ReportStorageException"/>, so it is never taken for a saved state. Use one instance for one message.
/// </summary>
public sealed class ReportProcessingStore : IReportProcessingStore
{
    private readonly ExpenseHubDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportProcessingStore"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core context.</param>
    public ReportProcessingStore(ExpenseHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<ReportRequest?> FindAsync(Guid reportId)
    {
        return GuardAsync(() => _dbContext.ReportRequests.AsNoTracking().FirstOrDefaultAsync(report => report.Id == reportId));
    }

    /// <inheritdoc />
    public Task<bool> TryStartAsync(Guid reportId, DateTimeOffset startedAtUtc)
    {
        return GuardAsync(async () =>
        {
            ReportRequest? report = await _dbContext.ReportRequests.FirstOrDefaultAsync(candidate => candidate.Id == reportId);

            if (report is null || report.Status != ReportStatus.Pending)
            {
                return false;
            }

            report.Status = ReportStatus.Processing;
            report.StartedAtUtc = startedAtUtc;

            return await SaveIfUnchangedAsync();
        });
    }

    /// <inheritdoc />
    public Task<bool> TryCompleteAsync(Guid reportId, string resultJson, DateTimeOffset completedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);

        return GuardAsync(async () =>
        {
            ReportRequest? report = await _dbContext.ReportRequests.FirstOrDefaultAsync(candidate => candidate.Id == reportId);

            if (report is null || report.Status != ReportStatus.Processing)
            {
                return false;
            }

            report.Status = ReportStatus.Completed;
            report.CompletedAtUtc = completedAtUtc;
            report.ResultJson = resultJson;

            return await SaveIfUnchangedAsync();
        });
    }

    /// <inheritdoc />
    public Task<bool> TryFailAsync(Guid reportId, string errorCode, string errorMessage, DateTimeOffset completedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return GuardAsync(async () =>
        {
            ReportRequest? report = await _dbContext.ReportRequests.FirstOrDefaultAsync(candidate => candidate.Id == reportId);

            if (report is null || report.Status is not (ReportStatus.Pending or ReportStatus.Processing))
            {
                return false;
            }

            report.Status = ReportStatus.Failed;
            report.CompletedAtUtc = completedAtUtc;
            report.ErrorCode = errorCode;
            report.ErrorMessage = errorMessage;

            return await SaveIfUnchangedAsync();
        });
    }

    private static async Task<T> GuardAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (SqliteTransientErrors.IsTransient(exception))
        {
            throw new ReportStorageException("The report storage is temporarily unavailable.", exception);
        }
    }

    private async Task<bool> SaveIfUnchangedAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();

            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The state is no longer the one that was read: another attempt finished first and nothing was written.
            _dbContext.ChangeTracker.Clear();

            return false;
        }
    }
}
