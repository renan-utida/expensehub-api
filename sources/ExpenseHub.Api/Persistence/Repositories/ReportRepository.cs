using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IReportRepository"/>. Every read of a request is limited to its requester inside the
/// query, and every read is without tracking, so nothing is loaded that the caller may not see and nothing is changed by reading.
/// </summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly ExpenseHubDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core context.</param>
    public ReportRepository(ExpenseHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(ReportRequest report)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            _dbContext.ReportRequests.Add(report);
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            throw new ReportStorageException("The report request could not be saved.", exception);
        }
        catch (SqliteException exception)
        {
            throw new ReportStorageException("The report request could not be saved.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportRequest>> ListByRequesterAsync(string requesterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

        List<ReportRequest> reports = await _dbContext.ReportRequests
            .AsNoTracking()
            .Where(report => report.RequesterId == requesterId)
            .OrderByDescending(report => report.CreatedAtUtc)
            .ThenByDescending(report => report.Id)
            .ToListAsync();

        return reports;
    }

    /// <inheritdoc />
    public Task<ReportRequest?> FindByRequesterAsync(Guid id, string requesterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

        return _dbContext.ReportRequests
            .AsNoTracking()
            .Where(report => report.RequesterId == requesterId)
            .FirstOrDefaultAsync(report => report.Id == id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportExpenseLine>> ListExpenseLinesAsync(ReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            List<ReportExpenseLine> lines = await ReportCalculator
                .Select(_dbContext.Expenses.AsNoTracking(), query)
                .ToListAsync();

            return lines;
        }
        catch (Exception exception) when (SqliteTransientErrors.IsTransient(exception))
        {
            throw new ReportStorageException("The expenses of the report could not be read now.", exception);
        }
    }
}
