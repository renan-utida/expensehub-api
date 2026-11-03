using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Reports;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Hand-written in-memory fake of <see cref="IReportRepository"/>; it never touches a database. Like the real repository, it
/// only finds a request inside the requester it is given, and it reads the expenses with the same
/// <see cref="ReportCalculator.Select"/> rule. It counts the reads of expenses, so a test can prove that following a report
/// never reads an expense again.
/// </summary>
internal sealed class FakeReportRepository : IReportRepository
{
    private readonly List<ReportRequest> _reports = new();

    /// <summary>Gets the expenses the report calculation reads. A test may change them at any time.</summary>
    public List<Expense> Expenses { get; } = new();

    /// <summary>Gets the requests in the repository.</summary>
    public IReadOnlyList<ReportRequest> Reports => _reports;

    /// <summary>Gets how many times a request was added.</summary>
    public int AddCalls { get; private set; }

    /// <summary>Gets how many times requests were listed.</summary>
    public int ListCalls { get; private set; }

    /// <summary>Gets how many times a request was searched.</summary>
    public int FindCalls { get; private set; }

    /// <summary>Gets how many times the expenses of a report were read.</summary>
    public int ExpenseReads { get; private set; }

    /// <summary>Gets or sets a value indicating whether adding a request fails as if the database were unavailable.</summary>
    public bool FailOnAdd { get; set; }

    /// <summary>Adds a request directly, without counting it as an addition made by the service.</summary>
    /// <param name="report">The request.</param>
    public void Given(ReportRequest report)
    {
        _reports.Add(report);
    }

    /// <inheritdoc />
    public Task AddAsync(ReportRequest report)
    {
        AddCalls++;

        if (FailOnAdd)
        {
            throw new ReportStorageException("Simulated storage failure.", new InvalidOperationException("The database is unavailable."));
        }

        _reports.Add(report);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportRequest>> ListByRequesterAsync(string requesterId)
    {
        ListCalls++;

        IReadOnlyList<ReportRequest> own = _reports
            .Where(report => report.RequesterId == requesterId)
            .OrderByDescending(report => report.CreatedAtUtc)
            .ThenByDescending(report => report.Id)
            .ToList();

        return Task.FromResult(own);
    }

    /// <inheritdoc />
    public Task<ReportRequest?> FindByRequesterAsync(Guid id, string requesterId)
    {
        FindCalls++;

        return Task.FromResult(_reports.Where(report => report.RequesterId == requesterId).FirstOrDefault(report => report.Id == id));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportExpenseLine>> ListExpenseLinesAsync(ReportQuery query)
    {
        ExpenseReads++;

        IReadOnlyList<ReportExpenseLine> lines = ReportCalculator.Select(Expenses.AsQueryable(), query).ToList();

        return Task.FromResult(lines);
    }
}
